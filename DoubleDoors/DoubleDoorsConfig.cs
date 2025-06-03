using OnixRuntime.Api.OnixClient;

namespace DoubleDoors {
    public partial class DoubleDoorsConfig : OnixModuleSettingRedirector {
        [Value(true)]
        public partial bool EnableDoors { get; set; }
        
        [Value(true)]
        public partial bool EnableTrapdoors { get; set; }
        
        [Value(true)]
        public partial bool EnableFenceGates { get; set; }
        
        [Value(true)]
        public partial bool EnableRecursiveOpening { get; set; }
        
        [Value(5)]
        [MinMax(1, 20)]
        public partial int RecursiveOpeningMaxDistance { get; set; }
        
        [Value(true)]
        public partial bool EnableModIncompatibilityCheck { get; set; }
    }
}