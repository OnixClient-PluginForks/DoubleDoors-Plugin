using System.Globalization;
using OnixRuntime.Api;
using OnixRuntime.Api.Entities;
using OnixRuntime.Api.Maths;
using OnixRuntime.Api.NBT;
using OnixRuntime.Api.Rendering;
using OnixRuntime.Api.World;
using OnixRuntime.Plugin;

namespace DoubleDoors {
    public class DoubleDoors : OnixPluginBase {
        public static DoubleDoors Instance { get; private set; } = null!;
        public static DoubleDoorsConfig Config { get; private set; } = null!;

        public DoubleDoors(OnixPluginInitInfo initInfo) : base(initInfo) {
            Instance = this;
            // If you can clean up what the plugin leaves behind manually, please do not unload the plugin when disabling.
            DisablingShouldUnloadPlugin = false;
#if DEBUG
           // WaitForDebuggerToBeAttached();
#endif
        }

        private static bool _isDoubleDoor;
        private static readonly List<BlockPos> DoorsToOpen = [];
        private static bool _cancelNextDoorNoise;
        private static bool _isProcessingDoubleDoor;

        protected override void OnLoaded() {
            Config = new DoubleDoorsConfig(PluginDisplayModule);
            Onix.Events.Player.BuildBlock += PlayerOnBuildBlock;
            Onix.Events.Rendering.RenderScreenGame += RenderingOnRenderScreen;
            Onix.Events.Audio.SoundPlayedInWorld += AudioOnSoundPlayedInWorld;
        }

        private bool AudioOnSoundPlayedInWorld(string sound, Vec3 position, float volume, float pitch) {
            if (sound.Contains("wooden_door") && _cancelNextDoorNoise) {
                _cancelNextDoorNoise = false;
                return true;
            }
            return false;
        }

        private void RenderingOnRenderScreen(RendererGame gfx, float delta, string screenName, bool isHudHidden, bool isClientUi) {
            if (_isDoubleDoor && DoorsToOpen.Count > 0) {
                List<BlockPos> doorsToProcess = new(DoorsToOpen);
                DoorsToOpen.Clear();
                _isDoubleDoor = false;
                
                _isProcessingDoubleDoor = true;
                foreach (BlockPos doorPos in doorsToProcess) {
                    Onix.LocalPlayer!.BuildBlock(doorPos, 0);
                }
                _isProcessingDoubleDoor = false;
                _cancelNextDoorNoise = true;
            }
        }
        private bool PlayerOnBuildBlock(LocalPlayer player, BlockPos position, BlockFace face) {
            if (_isProcessingDoubleDoor) {
                return false;
            }
            
            if (!TryGetDoorStatesAtPosition(position, out DoorInfo doorInfo)) {
                return false;
            }
            
            BlockPos lowerDoorPos = doorInfo.IsUpperBlock ? new BlockPos(position.X, position.Y - 1, position.Z) : position;
            
            BlockPos? adjacentPos = GetAdjacentDoorPosition(lowerDoorPos, doorInfo.CardinalDirection, doorInfo.IsRightHinge);
            if (adjacentPos is null || !IsValidDoubleDoor(adjacentPos.Value, doorInfo)) return false;
            if (!TryGetDoorStatesAtPosition(adjacentPos.Value, out DoorInfo adjacentDoorInfo)) return false;
            bool mainDoorWillBeOpen = !doorInfo.IsOpen;

            if (adjacentDoorInfo.IsOpen == mainDoorWillBeOpen) return false;
            _isDoubleDoor = true;
            DoorsToOpen.Add(adjacentPos.Value);

            return false;
        }

        private bool TryGetDoorStates(ObjectTag blockState, out DoorInfo doorInfo) {
            doorInfo = new DoorInfo();
            
            if (!blockState.Value.TryGetValue("states", out NbtTag? statesTag) || statesTag is not ObjectTag doorStates) {
                return false;
            }

            if (doorStates.Value.TryGetValue("door_hinge_bit", out NbtTag? hingeTag) && hingeTag is ByteTag hingeBit) {
                doorInfo.IsRightHinge = hingeBit.Value == 1;
            }

            if (doorStates.Value.TryGetValue("minecraft:cardinal_direction", out NbtTag? directionTag) && directionTag is StringTag direction) {
                doorInfo.CardinalDirection = direction.Value;
            }

            if (doorStates.Value.TryGetValue("open_bit", out NbtTag? openTag) && openTag is ByteTag openBit) {
                doorInfo.IsOpen = openBit.Value == 1;
            }

            if (doorStates.Value.TryGetValue("upper_block_bit", out NbtTag? upperTag) && upperTag is ByteTag upperBit) {
                doorInfo.IsUpperBlock = upperBit.Value == 1;
            }
            
            return !string.IsNullOrEmpty(doorInfo.CardinalDirection);
        }

        private bool TryGetDoorStatesAtPosition(BlockPos position, out DoorInfo doorInfo) {
            Block block = Onix.LocalPlayer!.Dimension.Region.GetBlock(position);
            
            if (!TryGetDoorStates(block.State, out doorInfo)) {
                return false;
            }

            BlockPos lowerBlockPos, upperBlockPos;
            
            if (doorInfo.IsUpperBlock) {
                upperBlockPos = position;
                lowerBlockPos = new BlockPos(position.X, position.Y - 1, position.Z);
            } else {
                lowerBlockPos = position;
                upperBlockPos = new BlockPos(position.X, position.Y + 1, position.Z);
            }
            
            Block lowerBlock = Onix.LocalPlayer.Dimension.Region.GetBlock(lowerBlockPos);
            Block upperBlock = Onix.LocalPlayer.Dimension.Region.GetBlock(upperBlockPos);
            
            if (!TryGetDoorStates(lowerBlock.State, out DoorInfo lowerDoorInfo) || !TryGetDoorStates(upperBlock.State, out DoorInfo upperDoorInfo)) {
                return false;
            }
            
            doorInfo.IsOpen = lowerDoorInfo.IsOpen;
            doorInfo.CardinalDirection = lowerDoorInfo.CardinalDirection;
            doorInfo.IsRightHinge = upperDoorInfo.IsRightHinge;
            doorInfo.IsUpperBlock = doorInfo.IsUpperBlock;

            return true;
        }

        private BlockPos? GetAdjacentDoorPosition(BlockPos doorPos, string cardinalDirection, bool isRightHinge) {
            return cardinalDirection switch {
                "east" => isRightHinge ? new BlockPos(doorPos.X - 1, doorPos.Y, doorPos.Z) : new BlockPos(doorPos.X + 1, doorPos.Y, doorPos.Z),
                "west" => isRightHinge ? new BlockPos(doorPos.X + 1, doorPos.Y, doorPos.Z) : new BlockPos(doorPos.X - 1, doorPos.Y, doorPos.Z),
                "south" => isRightHinge ? new BlockPos(doorPos.X, doorPos.Y, doorPos.Z - 1) : new BlockPos(doorPos.X, doorPos.Y, doorPos.Z + 1),
                "north" => isRightHinge ? new BlockPos(doorPos.X, doorPos.Y, doorPos.Z + 1) : new BlockPos(doorPos.X, doorPos.Y, doorPos.Z - 1),
                _ => null
            };
        }

        private bool IsValidDoubleDoor(BlockPos adjacentPos, DoorInfo currentDoorInfo) {
            if (!TryGetDoorStatesAtPosition(adjacentPos, out DoorInfo adjacentDoorInfo)) {
                return false;
            }
            
            if (currentDoorInfo.CardinalDirection != adjacentDoorInfo.CardinalDirection) {
                return false;
            }

            if (currentDoorInfo.IsRightHinge == adjacentDoorInfo.IsRightHinge) {
                return false;
            }

            return true;
        }

        private struct DoorInfo {
            public bool IsUpperBlock { get; set; }
            public bool IsOpen { get; set; }
            public string CardinalDirection { get; set; }
            public bool IsRightHinge { get; set; }
        }

        protected override void OnEnabled() {

        }

        protected override void OnDisabled() {

        }

        protected override void OnUnloaded() {
            Onix.Events.Player.BuildBlock -= PlayerOnBuildBlock;
            Onix.Events.Rendering.RenderScreenGame -= RenderingOnRenderScreen;
            Onix.Events.Audio.SoundPlayedInWorld -= AudioOnSoundPlayedInWorld;

            Instance = null!;
            Config = null!;
        }
    }
}