using System;
using System.Collections.Generic;

namespace plugin_balance
{
    public class Class1 : IPlugin_Sync
    {
        public string Name => "balance";

        public bool NeedNotify => false;

        public bool NeedFullLog => false;

        MyJson.JsonNode_Object statejson;
        public void OnBeginBlock(MyJson.JsonNode_Object statejson)
        {
            statejson.SetDictValue("pluginname", "balance");
            this.statejson = statejson;
        }

        public void OnEndBlock()
        {

        }

        public void OnInit()
        {

        }

        public void OnSyncBlock(int block, MyJson.JsonNode_Object blockjson)
        {

        }

        public void OnSyncBlockTransAction(int block, MyJson.JsonNode_Object txjson)
        {
            var type = txjson["type"].AsString();
            var txid = txjson["txid"].AsString();
            var vin = txjson["vin"] as MyJson.JsonNode_Array;
            var vout = txjson["vout"] as MyJson.JsonNode_Array;
            if (type == "MinerTransaction")//挖矿交易
            {
                //return;
            }
            else if (type == "IssueTransaction")//分钱
            {

            }
            else if (type == "ClaimTransaction")//分钱
            {

            }
            else if (type == "EnrollmentTransaction")//申请记账人
            {

            }
            else if (type == "RegisterTransaction")//注册交易,产生新的资产种类
            {
                var asset = txjson["asset"].AsDict();
                RegAsset(txid, asset);
            }
            else if (type == "ContractTransaction")//交易
            {

            }
            else if (type == "PublishTransaction")//发布合约
            {

            }
            else if (type == "InvocationTransaction")//执行合约
            {

            }
            else
            {

            }

            if (vout.Count > 0 || vin.Count > 0)
            {
                //记录一笔交易
                SetTran(block, txid, type, vin, vout);
            }
            //销毁utxo
            foreach (var v in vin)
            {
                var usetxid = v.GetDictItem("txid").AsString();
                var usev = v.GetDictItem("vout").AsInt();
                DestoryUTXO(txid, usetxid, usev);
            }
            //制造utxo
            foreach (var v in vout)
            {
                var n = v.GetDictItem("n").AsInt();
                var asset = v.GetDictItem("asset").AsString();
                var value = v.GetDictItem("value").AsString();
                var address = v.GetDictItem("address").AsString();
                MakeUTXO(address, txid, n, asset, value);
            }
        }

        //销毁UTXO
        void DestoryUTXO(string txid, string usetxid, int n)
        {
            //取得uxto中的地址
            var txmap = statejson["txs"] as MyJson.JsonNode_Object; //txmap 相当于一个mongodb 仓库
            var vout = txmap[usetxid].AsDict()["vout"].AsList()[n].AsDict();
            var address = vout["address"].AsString();//这个钱给了谁

            var utxomap = statejson["utxo"] as MyJson.JsonNode_Object;
            var utxoaddr = utxomap[address].AsDict(); //utxomap 相当于一个mongodb 仓库

            var key = usetxid + n.ToString("x04");
            //utxoaddr.Remove(key);//直接删除一笔花费或者将他标记为已经花费
            var money = utxoaddr[key].AsDict();
            money.SetDictValue("use", txid);//标记为已花费
        }

        //制造UTXO
        void MakeUTXO(string address, string txid, int n, string asset, string value)
        {
            if (statejson.ContainsKey("utxo") == false)
                statejson["utxo"] = new MyJson.JsonNode_Object();

            var utxomap = statejson["utxo"] as MyJson.JsonNode_Object;

            if (utxomap.ContainsKey(address) == false)
            {
                utxomap[address] = new MyJson.JsonNode_Object();
            }
            var utxoaddr = utxomap[address].AsDict();

            var key = txid + n.ToString("x04");
            var money = new MyJson.JsonNode_Object();
            utxoaddr.SetDictValue(key, money);
            money.SetDictValue("use", "");//标记花费有效，方便快速统计
            var assetName = GetAssetName(asset);
            money.SetDictValue("asset", assetName);//记录资产名，方便统计
            money.SetDictValue("value", value);//记录资产数量，方便统计
        }

        //记录有花费的交易
        void SetTran(int block, string txid, string txtype, MyJson.JsonNode_Array vin, MyJson.JsonNode_Array vout)
        {
            if (statejson.ContainsKey("txs") == false)
                statejson["txs"] = new MyJson.JsonNode_Object();

            var txmap = statejson["txs"] as MyJson.JsonNode_Object;
            MyJson.JsonNode_Object trans = new MyJson.JsonNode_Object();
            txmap[txid] = trans;

            trans.SetDictValue("block", block);
            trans.SetDictValue("type", txtype);
            trans.SetDictValue("in", vin);
            trans.SetDictValue("vout", vout);
        }

        //注册资源
        void RegAsset(string txid, MyJson.JsonNode_Object asset)
        {
            if (statejson.ContainsKey("assets") == false)
                statejson["assets"] = new MyJson.JsonNode_Array();
            while (statejson["assets"].AsList().Count < 2)
            {
                statejson["assets"].AsList().Add(new MyJson.JsonNode_Object());
            }

            var hashmap2asset = statejson["assets"].AsList()[0].AsDict();
            hashmap2asset[txid] = asset.Clone();


            Dictionary<string, string> _names = new Dictionary<string, string>();
            var names = asset["name"].AsList();
            string __name = "";
            foreach (MyJson.JsonNode_Object name in names)
            {
                var key = name["lang"].AsString();
                var value = name["name"].AsString();
                _names[key] = value;
                __name = value;
            }
            hashmap2asset[txid].SetDictValue("_name", __name);
            var assetname2hash = statejson["assets"].AsList()[1].AsDict();
            assetname2hash.SetDictValue(__name, txid);

        }

        string GetAssetName(string txid)
        {
            var hashmap2asset = statejson["assets"].AsList()[0].AsDict();
            if (hashmap2asset.ContainsKey(txid) == false)
            {
                return txid;
            }
            return hashmap2asset[txid].GetDictItem("_name").AsString();
        }
        string GetAssetHashByName(string name)
        {
            var assetname2hash = statejson["assets"].AsList()[1].AsDict();
            return assetname2hash[name].ToString();
        }
        MyJson.JsonNode_Object GetAsset(string txid)
        {
            var hashmap2asset = statejson["assets"].AsList()[0].AsDict();
            return hashmap2asset[txid].AsDict();
        }
        MyJson.JsonNode_Object GetTran(string txid)
        {
            var txmap = statejson["txs"] as MyJson.JsonNode_Object;
            return txmap[txid].AsDict();
        }

        public void OnSyncFullLog(int block, string txid, string hexstring)
        {

        }

        public void OnSyncNotify(int block, MyJson.JsonNode_Array notes)
        {

        }

        public MyJson.IJsonNode RPC(string method, MyJson.JsonNode_Array args)
        {
            if (method == "getutxo")
            {
                var address = args[0].AsString();
                var utxomap = statejson["utxo"] as MyJson.JsonNode_Object;
                var utxoaddr = utxomap[address].AsDict().Clone(); //utxomap 相当于一个mongodb 仓库
                return utxoaddr;
            }
            if(method =="getassets")
            {
                var hashmap2asset = statejson["assets"].AsList()[0].AsDict().Clone();
                return hashmap2asset;
            }
            return null;

        }
    }
}
