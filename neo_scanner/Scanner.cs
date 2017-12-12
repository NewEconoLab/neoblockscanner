using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace neo_scanner
{
    public class Scanner
    {
        const string rpc_getblockcount = "getblockcount";
        const string rpc_getblock = "getblock";
        const string rpc_getnotifyinfo = "getnotifyinfo";
        const string rpc_getfullloginfo = "getfullloginfo";
        readonly string rpcurl;
        readonly string statepath;
        readonly string pluginpath;
        bool bExit = false;
        MyJson.JsonNode_Object largeState;
        public int processedBlock
        {
            get;
            private set;
        }
        public int remoteBlockHeight
        {
            get;
            private set;
        }
        SHA256 sha256;
        Dictionary<string, IPlugin_Sync> plugins = new Dictionary<string, IPlugin_Sync>();

        public Scanner(MyJson.JsonNode_Object config)
        {
            var strurl = config["rpcurl"].AsString();
            if (strurl.Last() != '/') strurl += "/";
            rpcurl = config["rpcurl"].AsString();
            statepath = config["statepath"].AsString();
            pluginpath = config["pluginpath"].AsString();
            if (System.IO.Directory.Exists(pluginpath) == false)
                System.IO.Directory.CreateDirectory(pluginpath);
            sha256 = SHA256.Create();

        }
        void LoadState()
        {
            processedBlock = 0;
            var highblock = System.IO.Path.Combine(statepath, "processedblock.json");
            try
            {
                if (System.IO.File.Exists(highblock) == false)
                    throw new Exception(highblock + " not found.");


                var info = MyJson.Parse(System.IO.File.ReadAllText(highblock)).AsDict();
                processedBlock = info["blockindex"].AsInt();

                var hash = Helper.HexToString(info["hash"].AsString());
                var stateFile = System.IO.Path.Combine(statepath, "block_" + processedBlock.ToString() + ".state.json");

                if (System.IO.File.Exists(stateFile) == false)
                    throw new Exception(stateFile + " not found.");
                var bytes = System.IO.File.ReadAllBytes(stateFile);
                var hashinjson = sha256.ComputeHash(bytes);
                if (Helper.BytesEqual(hash, hashinjson) == false)
                    throw new Exception("hash not match");


                largeState = MyJson.Parse(System.Text.Encoding.UTF8.GetString(bytes)) as MyJson.JsonNode_Object;
            }
            catch (Exception err)
            {
                Console.WriteLine("error LoadState:" + err.Message);
                largeState = new MyJson.JsonNode_Object();
                processedBlock = -1;
            }
            Console.WriteLine("start block=" + processedBlock);
        }
        bool bNeedFullLog = false;
        bool bNeedNotify = false;
        void LoadPlugin()
        {

            var fullpath = System.IO.Path.GetFullPath(pluginpath);
            var files = System.IO.Directory.GetFiles(fullpath, "plugin_*.dll");
            foreach (var f in files)
            {
                var assem = System.Reflection.Assembly.LoadFile(f);
                foreach (var t in assem.ExportedTypes)
                {
                    var b = t.GetInterfaces().Contains(typeof(IPlugin_Sync));
                    if (b)
                    {
                        var plugin = t.Assembly.CreateInstance(t.FullName) as IPlugin_Sync;
                        var name = plugin.Name;
                        plugins.Add(name, plugin);
                    }
                }
            }
            foreach (var p in plugins)
            {
                p.Value.OnInit();
                if (p.Value.NeedFullLog)
                    bNeedFullLog = true;
                if (p.Value.NeedNotify)
                    bNeedNotify = true;
            }
        }

        int saveonce = 0;
        public void Save()
        {
            saveonce = 1;
        }
        void SaveState()
        {
            int block = this.processedBlock;
            {
                try
                {
                    if (System.IO.Directory.Exists(statepath) == false)
                        System.IO.Directory.CreateDirectory(statepath);
                    //write block file

                    var stateFile = System.IO.Path.Combine(statepath, "block_" + processedBlock.ToString() + ".state.json");
                    var highblock = System.IO.Path.Combine(statepath, "processedblock.json");
                    var bachightblock = System.IO.Path.Combine(statepath, "processedblock_bac" + block + ".json");

                    System.IO.File.Delete(stateFile);
                    System.IO.File.Delete(bachightblock);
                    System.IO.File.Delete(highblock);
                    var str = largeState.ToString();
                    var bytes = System.Text.Encoding.UTF8.GetBytes(str);

                    System.IO.File.WriteAllBytes(stateFile, bytes);
                    var hash = sha256.ComputeHash(bytes);

                    var info = new MyJson.JsonNode_Object();
                    info.SetDictValue("blockindex", block);
                    info.SetDictValue("hash", Helper.ToHexString(hash));
                    System.IO.File.WriteAllText(highblock, info.ToString());
                    System.IO.File.Copy(highblock, bachightblock);
                }
                catch (Exception err)
                {

                }
            }
        }
        System.Net.WebClient wc = new System.Net.WebClient();
        string MakeRpcUrl(string method, params MyJson.IJsonNode[] _params)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(rpcurl + "?jsonrpc=2.0&id=1&method=" + method + "&params=[");
            for (var i = 0; i < _params.Length; i++)
            {
                _params[i].ConvertToString(sb);
                if (i != _params.Length - 1)
                    sb.Append(",");
            }
            sb.Append("]");
            return sb.ToString();
        }
        //"\"?jsonrpc=2.0\"&id=4&method=getnotifyinfo&params=" [ 442484 ]
        public void Begin()
        {
            Console.WriteLine("rpcurl=" + rpcurl);
            Console.WriteLine("statepath=" + statepath);
            LoadPlugin();
            LoadState();
            System.Threading.Thread thread = new System.Threading.Thread(async () =>
             {
                 await CheckBlockLoop();
             });
            thread.Start();


            StartHttpServer();
        }
        async void StartHttpServer()
        {
            System.Net.HttpListener http = new HttpListener();
            http.Prefixes.Add("http://*:20666/");
            http.Start();
            while (true)
            {
                try
                {
                    var httpcontext = await http.GetContextAsync();
                    MyJson.JsonNode_Object response = new MyJson.JsonNode_Object();

                    try
                    {

                        string jsonrpc = httpcontext.Request.QueryString["jsonrpc"];
                        string id = httpcontext.Request.QueryString["id"];
                        string method = httpcontext.Request.QueryString["method"];
                        string _params = httpcontext.Request.QueryString["params"];

                        response["id"] = new MyJson.JsonNode_ValueString(id);
                        response["result"] = await DoReq(method, MyJson.Parse(_params) as MyJson.JsonNode_Array);
                    }
                    catch (Exception err)
                    {
                        response["error"] = new MyJson.JsonNode_ValueString(err.Message);
                    }
                    httpcontext.Response.ContentType = "application/json";
                    byte[] buffer = System.Text.Encoding.UTF8.GetBytes(response.ToString());
                    await httpcontext.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                    httpcontext.Response.Close();
                }
                catch
                {

                }
            }

        }
        async Task<MyJson.IJsonNode> DoReq(string method, MyJson.JsonNode_Array _params)
        {
            foreach(var p in plugins)
            {
                var result = p.Value.RPC(method, _params);
                if (result != null)
                    return result;
            }

            return null;
        }
        async Task CheckBlockLoop()
        {
            while (!bExit)
            {
                var gstr = MakeRpcUrl(rpc_getblockcount);
                var cstr = await wc.DownloadStringTaskAsync(gstr);
                var json = MyJson.Parse(cstr).AsDict();
                bool bError = json.ContainsKey("error");

                if (!bError)
                {
                    try
                    {
                        int height = json["result"].AsInt();
                        remoteBlockHeight = height - 1;
                        await SyncBlockTo(height - 1);
                    }
                    catch (Exception err)
                    {
                        Console.WriteLine(err.ToString());
                    }
                }
                if (bError)
                {//出错了
                    await Task.Delay(1000);
                    continue;
                }
            }
            SaveState();
            FullExit = true;
            return;
        }
        async Task SyncBlockTo(int height)
        {
            if (saveonce > 0)//每5000块备份一次
            {
                saveonce = 0;
                SaveState();
            }
            //var idwant = this.processedBlock + 1;
            for (var idwant = this.processedBlock + 1; idwant <= height; idwant++)
            {

                try
                {
                    {
                        //下载block
                        var gstr = MakeRpcUrl(rpc_getblock, new MyJson.JsonNode_ValueNumber(idwant), new MyJson.JsonNode_ValueNumber(1));
                        var cstr = await wc.DownloadStringTaskAsync(gstr);
                        var json = MyJson.Parse(cstr).AsDict();
                        bool bError = json.ContainsKey("error");
                        if (bError)
                            throw new Exception("rpc error");
                        SyncBlock(idwant, json["result"] as MyJson.JsonNode_Object);
                        //下载notify
                        if (bNeedNotify)
                        {
                            try
                            {
                                var gnstr = MakeRpcUrl(rpc_getnotifyinfo, new MyJson.JsonNode_ValueNumber(idwant));
                                var cnstr = await wc.DownloadStringTaskAsync(gnstr);
                                var jsonn = MyJson.Parse(cnstr).AsDict();
                                bool bErrorn = jsonn.ContainsKey("error");
                                if (bErrorn == false)
                                {
                                    SyncNotify(idwant, jsonn["result"] as MyJson.JsonNode_Array);
                                }
                            }
                            catch (Exception errnotify)
                            {

                            }
                        }
                        var txs = json["result"].AsDict()["tx"].AsList();
                        foreach (MyJson.JsonNode_Object tx in txs)
                        {
                            //处理交易
                            SyncBlockTransAction(idwant, tx);
                            if (bNeedFullLog)
                            {
                                //处理FullLog
                                try
                                {
                                    var txid = tx["txid"].AsString();
                                    var glstr = MakeRpcUrl(rpc_getfullloginfo, new MyJson.JsonNode_ValueString(txid));
                                    var clstr = await wc.DownloadStringTaskAsync(glstr);
                                    var jsonl = MyJson.Parse(clstr).AsDict();
                                    bool bErrorl = jsonl.ContainsKey("error");
                                    if (bErrorl == false)
                                    {
                                        SyncFullLog(idwant, txid, jsonl["result"].AsString());
                                    }
                                }
                                catch (Exception errfulllog)
                                {

                                }
                            }
                        }
                    }
                    this.processedBlock = idwant;

                    if (idwant % 10000 == 0 || saveonce > 0)//每5000块备份一次
                    {
                        saveonce = 0;
                        SaveState();
                    }


                    //idwant = this.processedBlock + 1;
                }
                catch (Exception err)
                {
                    Console.WriteLine("err in SyncBlockTo:" + err.ToString());
                    break;
                }
                if (bExit) return;
            }

        }
        void SyncBlock(int block, MyJson.JsonNode_Object blockjson)
        {
            largeState.SetDictValue("_blockid", block);
            foreach (var p in plugins)
            {

                MyJson.JsonNode_Object saves = null;
                if (largeState.ContainsKey(p.Key))
                    saves = largeState.GetDictItem(p.Key) as MyJson.JsonNode_Object;
                else
                {

                    saves = new MyJson.JsonNode_Object();
                    largeState[p.Key] = saves;
                }
                p.Value.OnBeginBlock(saves);
                p.Value.OnSyncBlock(block, blockjson);
            }
        }
        void SyncBlockTransAction(int block, MyJson.JsonNode_Object txjson)
        {
            foreach (var p in plugins)
            {
                var saves = largeState.GetDictItem(p.Key) as MyJson.JsonNode_Object;

                p.Value.OnSyncBlockTransAction(block, txjson);
            }
        }
        void SyncNotify(int block, MyJson.JsonNode_Array notes)
        {
            foreach (var p in plugins)
            {
                var saves = largeState.GetDictItem(p.Key) as MyJson.JsonNode_Object;
                p.Value.OnSyncNotify(block, notes);
            }
        }
        void SyncFullLog(int block, string txid, string hexstring)
        {
            foreach (var p in plugins)
            {
                var saves = largeState.GetDictItem(p.Key) as MyJson.JsonNode_Object;
                p.Value.OnSyncFullLog(block, txid, hexstring);
            }
        }
        public void Exit()
        {

            bExit = true;

        }
        public bool FullExit
        {
            get;
            private set;
        }
    }
}
