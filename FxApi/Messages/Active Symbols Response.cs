using System.Collections.Generic;

namespace FxApi
{
    public class ActiveSymbolsResponse
    {
        public List<ActiveSymbol> active_symbols { get; set; }
        public object echo_req { get; set; }
        public string msg_type { get; set; }
    }
}