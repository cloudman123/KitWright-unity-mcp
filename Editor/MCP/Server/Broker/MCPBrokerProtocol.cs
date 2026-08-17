// Copyright (C) KitWright. Licensed under MIT.

namespace KitWright.Editor.MCP.Server
{
    internal static class MCPBrokerProtocol
    {
        // v2: pull responses carry AcceptSseHeader (client's Accept: text/event-stream),
        //     push requests may carry ContentTypeHeader to override the client-facing
        //     response content type (used for SSE-piggybacked notifications).
        // v3: the broker checks the project pin in the request path -- a v2 broker left running
        //     would keep forwarding another project's requests, so the bump forces it to be
        //     replaced; and a tools/call that arrives while no Unity backend is attached comes
        //     back as an ordinary tool result telling the agent to retry, not a JSON-RPC error.
        public const int Version = 3;
        public const string Name = "kitwright-unity-mcp-broker";
        public const string HealthPath = "/_kitwright/broker/health";
        public const string AttachPath = "/_kitwright/broker/attach";
        public const string PullPath = "/_kitwright/broker/pull";
        public const string PushPath = "/_kitwright/broker/push";
        public const string DetachPath = "/_kitwright/broker/detach";
        public const string ShutdownPath = "/_kitwright/broker/shutdown";
        public const string TokenHeader = "X-KitWright-Broker-Token";
        public const string SessionHeader = "X-KitWright-Broker-Session";
        public const string ReqIdHeader = "X-KitWright-Broker-ReqId";
        public const string RedeliveryHeader = "X-KitWright-Broker-Redelivery";
        public const string BrokerHeader = "X-KitWright-Broker";
        public const string AcceptSseHeader = "X-KitWright-Broker-Accept-SSE";
        public const string ContentTypeHeader = "X-KitWright-Broker-Content-Type";
    }
}
