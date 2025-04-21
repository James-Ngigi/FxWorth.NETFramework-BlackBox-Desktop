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
        public string currentLevelId { get; set; }
        private int hierarchyLevelsCount;
        public int maxHierarchyDepth { get; private set; }
        private PhaseParameters phase1Params;
        private PhaseParameters phase2Params;
        private readonly TokenStorage storage;
        public bool IsInHierarchyMode { get; private set; } = false;
        internal int layer1CompletedLevels = 0;
        private Dictionary<string, AuthClient> levelClients = new Dictionary<string, AuthClient>();

        public HierarchyNavigator(decimal amountToBeRecovered, TradingParameters tradingParameters, PhaseParameters phase1Params, PhaseParameters phase2Params, Dictionary<int, CustomLayerConfig> customLayerConfigs, decimal initialStakeLayer1, TokenStorage storage)
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
                CreateHierarchy(amountToBeRecovered, tradingParameters, customLayerConfigs, initialStakeLayer1);
            }
        }

        public void AssignClientToLevel(string levelId, AuthClient client)
        {
            if (hierarchyLevels.ContainsKey(levelId))
            {
                levelClients[levelId] = client;
                LoadLevelTradingParameters(levelId, client, client.TradingParameters);
            }
        }

        public AuthClient GetClientForLevel(string levelId)
        {
            return levelClients.TryGetValue(levelId, out var client) ? client : null;
        }

        private void CreateHierarchy(decimal amountToBeRecovered, TradingParameters tradingParameters, Dictionary<int, CustomLayerConfig> customLayerConfigs, decimal initialStakeLayer1)
        {
            IsInHierarchyMode = true; // Enter hierarchy mode

            CreateLayer(1, amountToBeRecovered, tradingParameters, customLayerConfigs, initialStakeLayer1);

            for (int i = 2; i <= maxHierarchyDepth; i++)
            {
                if (ExistsCustomConfigForLayer(i, customLayerConfigs))
                {
                    decimal initialStakeForLayer = hierarchyLevels.TryGetValue($"{i - 1}.1", out var previousLayerLevel) ? previousLayerLevel.InitialStake : initialStakeLayer1;
                    CreateLayer(i, amountToBeRecovered, tradingParameters, customLayerConfigs, initialStakeForLayer);
                }
            }

            currentLevelId = "1.1";
        }

        public void CreateLayer(int layerNumber, decimal amountToBeRecovered, TradingParameters tradingParameters, Dictionary<int, CustomLayerConfig> customLayerConfigs, decimal initialStake)
        {
            CustomLayerConfig customConfig = GetCustomConfigForLayer(layerNumber, customLayerConfigs);

            int hierarchyLevelsForLayer = Math.Max(2, customConfig?.HierarchyLevels ?? hierarchyLevelsCount);
            decimal amountPerLevel = amountToBeRecovered / hierarchyLevelsForLayer;

            for (int i = 1; i <= hierarchyLevelsForLayer; i++)
            {
                string levelId = $"{layerNumber}.{i}";

                int? martingaleLevel = customConfig?.MartingaleLevel ?? phase2Params.MartingaleLevel;
                decimal? maxDrawdown = customConfig?.MaxDrawdown ?? phase2Params.MaxDrawdown;
                decimal? barrierOffset = customConfig?.BarrierOffset ?? phase2Params.Barrier;

                decimal levelInitialStake;
                if (customConfig?.InitialStake != null)
                {
                    levelInitialStake = customConfig.InitialStake.Value;
                }
                else if (layerNumber > 1)
                {
                    if (hierarchyLevels.TryGetValue($"{layerNumber - 1}.1", out var previousLayerLevel))
                    {
                        levelInitialStake = previousLayerLevel.InitialStake;
                    }
                    else
                    {
                        levelInitialStake = initialStake;
                    }
                }
                else
                {
                    levelInitialStake = initialStake;
                }

                HierarchyLevel newLevel = new HierarchyLevel(levelId, amountPerLevel, levelInitialStake, martingaleLevel, maxDrawdown, barrierOffset);
                hierarchyLevels[levelId] = newLevel;
                levelOrder.Add(levelId);

                decimal levelMaxDrawdown = customConfig?.MaxDrawdown ?? phase2Params.MaxDrawdown;

                if (amountPerLevel > levelMaxDrawdown && layerNumber < maxHierarchyDepth)
                {
                    CreateLayer(layerNumber + 1, amountPerLevel, tradingParameters, customLayerConfigs, levelInitialStake);
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
            public List<decimal> recoveryResults { get; set; } = new List<decimal>();
            public bool IsCompleted { get; set; }

            public HierarchyLevel(string levelId, decimal amountToBeRecovered, decimal initialStake, int? martingaleLevel, decimal? maxDrawdown, decimal? barrierOffset)
            {
                LevelId = levelId;
                AmountToBeRecovered = amountToBeRecovered;
                InitialStake = initialStake;
                MartingaleLevel = martingaleLevel;
                MaxDrawdown = maxDrawdown;
                BarrierOffset = barrierOffset;
                IsCompleted = false;
            }
        }

        public void MoveToNextLevel(AuthClient client)
        {
            logger.Info($"Exiting Level: {currentLevelId}");

            string[] parts = currentLevelId.Split('.');
            int currentLayer = parts.Length;
            int currentLevelNumber = int.Parse(parts.Last());

            if (currentLevelNumber < hierarchyLevelsCount)
            {
                currentLevelNumber++;
                parts[parts.Length - 1] = currentLevelNumber.ToString();
                currentLevelId = string.Join(".", parts);
                AssignClientToLevel(currentLevelId, client);
                logger.Info($"Entering Level: {currentLevelId}");
                return;
            }

            if (currentLayer == 1)
            {
                layer1CompletedLevels++;
                if (layer1CompletedLevels >= hierarchyLevelsCount)
                {
                    IsInHierarchyMode = false;
                    currentLevelId = "0";
                    layer1CompletedLevels = 0;
                    logger.Info("Layer 1 recovered. Returning to root level trading.");
                    return;
                }
                else
                {
                    currentLevelNumber = layer1CompletedLevels + 1;
                    currentLevelId = $"1.{currentLevelNumber}";
                    AssignClientToLevel(currentLevelId, client);
                    logger.Info($"Entering Level: {currentLevelId}");
                    return;
                }
            }

            parts = parts.Take(parts.Length - 1).ToArray();
            currentLevelId = string.Join(".", parts);
            AssignClientToLevel(currentLevelId, client);
            logger.Info($"Moving up to parent level: {currentLevelId}");
        }

        public void LoadLevelTradingParameters(string levelId, AuthClient client, TradingParameters tradingParameters)
        {
            if (!hierarchyLevels.TryGetValue(levelId, out HierarchyLevel level))
            {
                logger.Error($"Level {levelId} not found in hierarchy");
                return;
            }

            CustomLayerConfig customConfig = GetCustomConfigForLayer(int.Parse(levelId.Split('.')[0]), storage.customLayerConfigs);

            tradingParameters.MartingaleLevel = customConfig?.MartingaleLevel ?? level.MartingaleLevel ?? (levelId.StartsWith("1.") ? phase2Params.MartingaleLevel : phase1Params.MartingaleLevel);
            tradingParameters.MaxDrawdown = customConfig?.MaxDrawdown ?? level.MaxDrawdown ?? (levelId.StartsWith("1.") ? phase2Params.MaxDrawdown : phase1Params.MaxDrawdown);
            tradingParameters.Barrier = customConfig?.BarrierOffset ?? level.BarrierOffset ?? (levelId.StartsWith("1.") ? phase2Params.Barrier : phase1Params.Barrier);
            tradingParameters.TempBarrier = level.BarrierOffset ?? tradingParameters.Barrier;
            tradingParameters.AmountToBeRecoverd = level.AmountToBeRecovered;

            if (tradingParameters.DynamicStake == 0 || tradingParameters.DynamicStake == tradingParameters.Stake)
            {
                tradingParameters.DynamicStake = level.InitialStake;
            }

            // Update the level's recovery results with the client's current recovery results
            level.recoveryResults = new List<decimal>(tradingParameters.RecoveryResults);
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

        public decimal GetLayer1TotalAmountToBeRecovered()
        {
            decimal total = 0;
            foreach (var level in hierarchyLevels.Values)
            {
                if (level.LevelId.StartsWith("1."))
                {
                    total += level.AmountToBeRecovered;
                }
            }
            return total;
        }
    }
}