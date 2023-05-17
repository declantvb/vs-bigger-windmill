using System;
using Vintagestory.API.Common;

namespace biggerwindmill.src
{
    public class BiggerWindmills : ModSystem
    {
        public static ModConfig config;
        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            api.RegisterBlockClass("BlockLargeWindmillRotor", typeof(BlockLargeWindmillRotor));
            api.RegisterBlockEntityBehaviorClass("LargeWindmillRotorEntityBehavior", typeof(BEBehaviorLargeWindmillRotor));

            try
            {
                config = api.LoadModConfig<ModConfig>("biggerwindmill.json");
                if (config == null)
                {
                    config = new ModConfig();
                    api.StoreModConfig<ModConfig>(config, "biggerwindmill.json");
                }
                // Clamp config to acceptable values
                config.maxSails = Math.Max(1, Math.Min(config.maxSails, 8));
                config.torqueMultiplier = Math.Max(0.1f, config.torqueMultiplier);
            }
            catch (System.Exception)
            {
                api.Logger.Error("Could not load biggerwindmill config, using default values...");
                config = new ModConfig();
            }

        }
    }
}