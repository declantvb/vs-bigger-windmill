using System;
using System.Text;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using Vintagestory.GameContent.Mechanics;

namespace biggerwindmill.src
{
    public class BEBehaviorLargeWindmillRotor : BEBehaviorMPRotor
    {
        WeatherSystemBase weatherSystem;
        double windSpeed;

        int sailLength = 0;
        public int SailLength => sailLength;

        private AssetLocation sound;
        protected override AssetLocation Sound => sound;
        protected override float GetSoundVolume() => (0.5f + 0.5f * (float)windSpeed) * sailLength / 3f;

        protected override float Resistance => 0.003f;
        protected override double AccelerationFactor => 0.05d;
        protected override float TargetSpeed => (float)Math.Min(0.6f, windSpeed);
        protected override float TorqueFactor => sailLength / 4f * BiggerWindmills.config.torqueMultiplier;

        public BEBehaviorLargeWindmillRotor(BlockEntity blockentity) : base(blockentity)
        {
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);
            this.sound = new AssetLocation("sounds/effect/swoosh");
            weatherSystem = Api.ModLoader.GetModSystem<WeatherSystemBase>();
            Blockentity.RegisterGameTickListener(CheckWindSpeed, 1000);
        }

        private void CheckWindSpeed(float dt)
        {
            windSpeed = weatherSystem.WeatherDataSlowAccess.GetWindSpeed(Blockentity.Pos.ToVec3d());
            if (Api.World.BlockAccessor.GetLightLevel(Blockentity.Pos, EnumLightLevelType.OnlySunLight) < 5 && Api.World.Config.GetString("undergroundWindmills", "false") != "true") windSpeed = 0;

            if (Api.Side == EnumAppSide.Server && sailLength > 0 && Api.World.Rand.NextDouble() < 0.2)
            {
                if (obstructed(sailLength + 1))
                {
                    Api.World.PlaySoundAt(new AssetLocation("sounds/effect/toolbreak"), Position.X + 0.5, Position.Y + 0.5, Position.Z + 0.5, null, false, 20, 1f);
                    while (sailLength-- > 0)
                    {
                        ItemStack stacks = new ItemStack(Api.World.GetItem(new AssetLocation("biggerwindmill:largesail")), 4);
                        Api.World.SpawnItemEntity(stacks, Blockentity.Pos.ToVec3d().Add(0.5, 0.5, 0.5));
                    }
                    sailLength = 0;
                    Blockentity.MarkDirty(true);
                    this.network.updateNetwork(manager.getTickNumber());
                }
            }
        }

        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            while (sailLength-- > 0)
            { 
                ItemStack stacks = new ItemStack(Api.World.GetItem(new AssetLocation("biggerwindmill:largesail")), 4);
                Api.World.SpawnItemEntity(stacks, Blockentity.Pos.ToVec3d().Add(0.5, 0.5, 0.5));
            }

            base.OnBlockBroken(byPlayer);
        }

        internal bool OnInteract(IPlayer byPlayer)
        {
            if (sailLength >= BiggerWindmills.config.maxSails) return false;

            ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (slot.Empty || slot.StackSize < 4) return false;

            ItemStack sailStack = new ItemStack(Api.World.GetItem(new AssetLocation("biggerwindmill:largesail")));
            if (!slot.Itemstack.Equals(Api.World, sailStack, GlobalConstants.IgnoredStackAttributes)) return false;

            int len = sailLength + 2;

            if (obstructed(len))
            {
                if (Api.Side == EnumAppSide.Client)
                {
                    (Api as ICoreClientAPI).TriggerIngameError(this, "notenoughspace", Lang.Get("Cannot add more sails. Make sure there's space for the sails to rotate freely"));
                }
                return false;
            }

            if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                slot.TakeOut(4);
                slot.MarkDirty();
            }
            sailLength++;
            updateShape(Api.World);

            Blockentity.MarkDirty(true);
            return true;
        }

        bool obstructed(int len)
        {
            BlockPos tmpPos = new BlockPos();

            for (int dxz = -len; dxz <= len; dxz++)
            {
                for (int dy = -len; dy <= len; dy++)
                {
                    if (dxz == 0 && dy == 0) continue;
                    if (len > 1 && Math.Abs(dxz) == len && Math.Abs(dy) == len) continue;

                    int dx = ownFacing.Axis == EnumAxis.Z ? dxz : 0;
                    int dz = ownFacing.Axis == EnumAxis.X ? dxz : 0;
                    tmpPos.Set(Position.X + dx, Position.Y + dy, Position.Z + dz);

                    Block block = Api.World.BlockAccessor.GetBlock(tmpPos);
                    Cuboidf[] collBoxes = block.GetCollisionBoxes(Api.World.BlockAccessor, tmpPos);
                    if (collBoxes != null && collBoxes.Length > 0 && !(block is BlockSnowLayer) && !(block is BlockSnow))
                    {
                        
                        return true;
                    }
                }
            }

            return false;
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            sailLength = tree.GetInt("sailLength");

            base.FromTreeAttributes(tree, worldAccessForResolve);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            tree.SetInt("sailLength", sailLength);
            base.ToTreeAttributes(tree);
        }

        protected override void updateShape(IWorldAccessor worldForResolve)
        {
            if (worldForResolve.Side != EnumAppSide.Client || Block == null)
            {
                return;
            }

            if (sailLength == 0)
            {
                Shape = new CompositeShape()
                {
                    Base = new AssetLocation("biggerwindmill:block/largewindmillrotor"),
                    rotateY = Block.Shape.rotateY
                };
            }
            else
            {
                Shape = new CompositeShape()
                {
                    Base = new AssetLocation("biggerwindmill:block/largewindmill-" + sailLength + "blade"),
                    rotateY = Block.Shape.rotateY
                };
            }
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
        {
            base.GetBlockInfo(forPlayer, sb);

            sb.AppendLine(string.Format(Lang.Get("Wind speed: {0}%", (int)(100*windSpeed))));
            sb.AppendLine(Lang.Get("Sails power output: {0} kN", (int)(sailLength / 5f * 100f * BiggerWindmills.config.torqueMultiplier)));
        }
    }
}