using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public interface IPlugin_Sync
{
    string Name
    { get; }
    void OnInit();
    void OnBeginBlock(MyJson.JsonNode_Object statejson);
    void OnSyncBlock(int block, MyJson.JsonNode_Object blockjson);
    void OnSyncBlockTransAction(int block, MyJson.JsonNode_Object txjson);
    bool NeedNotify
    {
        get;
    }

    void OnSyncNotify(int block, MyJson.JsonNode_Array notes);
    bool NeedFullLog
    {
        get;
    }
    void OnSyncFullLog(int block, string txid, string hexstring);
    void OnEndBlock();
    MyJson.IJsonNode RPC(string call, MyJson.JsonNode_Array _params);
}

