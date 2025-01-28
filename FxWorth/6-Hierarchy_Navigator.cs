using System;
using System.Collections.Generic;
using System.Linq;
using FxApi;
using FxApi.Connection;
using NLog;

namespace FxWorth.Hierarchy
{
    public class HierarchyNavigator
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private Dictionary<string, HierarchyLevel> hierarchyLevels;
        private List<string> levelOrder;
        public string currentLevelId;
        private int hierarchyLevelsCount;
        public int maxHierarchyDepth;
        private PhaseParameters phase1Params;
        private PhaseParameters phase2Params;
        private readonly TokenStorage storage;

        public HierarchyNavigator(decimal amountToBeRecovered, TradingParameters tradingParameters, PhaseParameters phase1Params, 
        PhaseParameters phase2Params, Dictionary<int, CustomLayerConfig> customLayerConfigs, TokenStorage storage)
        {
            this.hierarchyLevelsCount = tradingParameters.HierarchyLevels;
            this.maxHierarchyDepth = tradingParameters.MaxHierarchyDepth;
            this.phase1Params = phase1Params;
            this.phase2Params = phase2Params;
            this.storage = storage;

            hierarchyLevels = new Dictionary<string, HierarchyLevel>();
            levelOrder = new List<string>();

            if (amountToBeRecovered > phase1Params.MaxDrawdown)
            {
                CreateHierarchy(amountToBeRecovered, tradingParameters, customLayerConfigs);
            }
        }

        private void CreateHierarchy(decimal amountToBeRecovered, TradingParameters tradingParameters, Dictionary<int, CustomLayerConfig> customLayerConfigs)
        {
            decimal initialStakeLayer1 = Stake_TXT2.Value; // Get initial stake for Layer 1 from UI
            CreateLayer(1, amountToBeRecovered, tradingParameters, customLayerConfigs, initialStakeLayer1);

            for (int i = 2; i <= maxHierarchyDepth; i++)
            {
                if (ExistsCustomConfigForLayer(i, customLayerConfigs))
                {
                    // For layers beyond 1, use the InitialStake from the first level of the previous layer if it exists, otherwise use the Phase 2 InitialStake
                    decimal initialStakeForLayer = hierarchyLevels.TryGetValue($"{i - 1}.1", out var previousLayerLevel) ? previousLayerLevel.InitialStake : Stake_TXT2.Value;

                    CreateLayer(i, amountToBeRecovered, tradingParameters, customLayerConfigs, initialStakeForLayer);
                }
            }

            currentLevelId = "1.1";
        }


        public void CreateLayer(int layerNumber, decimal amountToBeRecovered, TradingParameters tradingParameters, Dictionary<int, CustomLayerConfig> customLayerConfigs, int recoveryTradesTarget)
        {
            CustomLayerConfig customConfig = GetCustomConfigForLayer(layerNumber, customLayerConfigs);

            int hierarchyLevelsForLayer = Math.Max(2, customConfig?.HierarchyLevels ?? hierarchyLevelsCount); // Ensure at least 2 levels
            decimal amountPerLevel = amountToBeRecovered / hierarchyLevelsForLayer;

            for (int i = 1; i <= hierarchyLevelsForLayer; i++)
            {
                string levelId = $"{layerNumber}.{i}";

                // Calculate InitialStake for each level independently
                int levelRecoveryTradesTarget = customConfig?.RecoveryTradesTarget ?? recoveryTradesTarget;
                decimal levelInitialStake = CalculateInitialStake(amountPerLevel, tradingParameters.Stake, tradingParameters.PreviousProfit, levelRecoveryTradesTarget);


                int? martingaleLevel = customConfig?.CustomMartingaleLevel ?? phase2Params.MartingaleLevel;
                decimal? maxDrawdown = customConfig?.CustomMaxDrawdown ?? phase2Params.MaxDrawdown;
                decimal? barrierOffset = customConfig?.CustomBarrierOffset ?? phase2Params.Barrier;


                HierarchyLevel newLevel = new HierarchyLevel(levelId, amountPerLevel, levelInitialStake, martingaleLevel, maxDrawdown, barrierOffset);
                hierarchyLevels[levelId] = newLevel;
                levelOrder.Add(levelId);

                decimal levelMaxDrawdown = customConfig?.CustomMaxDrawdown ?? phase2Params.MaxDrawdown;

                // Use level-specific maxDrawdown and recoveryTradesTarget for nested layer creation
                if (amountPerLevel > levelMaxDrawdown && layerNumber < maxHierarchyDepth)
                {
                    CreateLayer(layerNumber + 1, amountPerLevel, tradingParameters, customLayerConfigs, levelRecoveryTradesTarget); // Pass recoveryTradesTarget
                }
            }
        }

        public class HierarchyLevel
        {
            public string LevelId { get; set; }
            public decimal AmountToBeRecovered { get; set; }
            public decimal InitialStake { get; set; }
            public int? MartingaleLevel { get; set; }
            public decimal? MaxDrawdown { get; set; }
            public decimal? BarrierOffset { get; set; }


            public HierarchyLevel(string levelId, decimal amountToBeRecovered, decimal initialStake, int? martingaleLevel, decimal? maxDrawdown, decimal? barrierOffset)
            {
                LevelId = levelId;
                AmountToBeRecovered = amountToBeRecovered;
                InitialStake = initialStake;
                MartingaleLevel = martingaleLevel;
                MaxDrawdown = maxDrawdown;
                BarrierOffset = barrierOffset;
            }
        }

        public void MoveToNextLevel(AuthClient client)
        {
            logger.Info($"Exiting Level: {currentLevelId}");

            string nextLevelId = GenerateNextLevelId(currentLevelId, hierarchyLevelsCount);

            if (nextLevelId != null)
            {
                currentLevelId = nextLevelId;
                LoadLevelTradingParameters(currentLevelId, client, client.TradingParameters);
                logger.Info($"Entering Level: {currentLevelId}");
            }
            else
            {
                if (currentLevelId.Split('.').Length == 2)
                {
                    currentLevelId = "0";
                    logger.Info("Layer 1 recovered. Returning to root level.");
                }
                else
                {
                    string parentLevelId = GenerateParentLevelId(currentLevelId);
                    currentLevelId = parentLevelId;
                    LoadLevelTradingParameters(currentLevelId, client, client.TradingParameters);
                    logger.Info($"Moving up to parent level: {currentLevelId}");

                    MoveToNextLevel(client); // Recursive call
                }
            }
        }

        public void LoadLevelTradingParameters(string levelId, AuthClient client, TradingParameters tradingParameters)
        {
            HierarchyLevel level = hierarchyLevels[levelId];
            CustomLayerConfig customConfig = GetCustomConfigForLayer(int.Parse(levelId.Split('.')[0]), storage.customLayerConfigs); // Get custom config for the current layer

            // Prioritize custom parameters, then level parameters, then phase 2, then phase 1
            tradingParameters.MartingaleLevel = customConfig?.CustomMartingaleLevel ?? level.MartingaleLevel ?? (levelId.StartsWith("1.") ? phase2Params.MartingaleLevel : phase1Params.MartingaleLevel);
            tradingParameters.MaxDrawdown = customConfig?.CustomMaxDrawdown ?? level.MaxDrawdown ?? (levelId.StartsWith("1.") ? phase2Params.MaxDrawdown : phase1Params.MaxDrawdown);
            tradingParameters.Barrier = customConfig?.CustomBarrierOffset ?? level.BarrierOffset ?? (levelId.StartsWith("1.") ? phase2Params.Barrier : phase1Params.Barrier);
            tradingParameters.RecoveryTradesTarget = customConfig?.RecoveryTradesTarget ?? level.RecoveryTradesTarget ?? tradingParameters.RecoveryTradesTarget;
            tradingParameters.TempBarrier = level.BarrierOffset ?? tradingParameters.Barrier;
            tradingParameters.AmountToBeRecoverd = level.AmountToBeRecovered;

            if (tradingParameters.DynamicStake == 0 || tradingParameters.DynamicStake == tradingParameters.Stake)
            {
                tradingParameters.DynamicStake = level.InitialStake;
            }
        }


        private string GenerateNextLevelId(string currentLevelId, int hierarchyLevelsForLayer)
        {
            string[] parts = currentLevelId.Split('.');
            int currentLayer = parts.Length - 1;
            int currentLevelNumber = int.Parse(parts[currentLayer]);

            if (currentLevelNumber < hierarchyLevelsForLayer)
            {
                parts[currentLayer] = (currentLevelNumber + 1).ToString();
                return string.Join(".", parts);
            }
            else
            {
                return null;
            }
        }

        public HierarchyLevel GetCurrentLevel()
        {
            if (string.IsNullOrEmpty(currentLevelId))
            {
                logger.Warn("currentLevelId is null or empty. Cannot get current level.");
                return null;
            }

            if (hierarchyLevels.TryGetValue(currentLevelId, out HierarchyLevel currentLevel))
            {
                return currentLevel;
            }
            else
            {
                logger.Error($"Level with ID '{currentLevelId}' not found in hierarchy.");
                return null;
            }
        }

        private string GenerateParentLevelId(string currentLevelId)
        {
            return string.Join(".", currentLevelId.Split('.').Take(currentLevelId.Split('.').Length - 1));
        }


        private CustomLayerConfig GetCustomConfigForLayer(int layerNumber, Dictionary<int, CustomLayerConfig> customLayerConfigs)
        {
            if (customLayerConfigs.TryGetValue(layerNumber, out CustomLayerConfig config))
            {
                return config;
            }
            return null;
        }

        private bool ExistsCustomConfigForLayer(int layerNumber, Dictionary<int, CustomLayerConfig> customLayerConfigs)
        {
            return customLayerConfigs.ContainsKey(layerNumber);
        }

        private AuthClient GetClientForCurrentTrade(string token)
        {
            return storage.Clients.FirstOrDefault(x => x.Value.GetToken() == token).Value;
        }

        private decimal CalculateInitialStake(decimal amountToRecover, decimal baseStake, decimal previousProfit, int recoveryTradesTarget)
        {
            decimal targetProfitPerTrade = amountToRecover / recoveryTradesTarget;
            return Math.Round(targetProfitPerTrade * baseStake / previousProfit, 2);
        }
    }
}