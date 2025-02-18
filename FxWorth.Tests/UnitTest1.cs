using NUnit.Framework;
using Moq;
using FxApi.Connection;
using System.Collections.Generic;
using FxWorth.Hierarchy;
using FxApi;

namespace FxWorth.Tests
{
    [TestFixture]
    public class HierarchyNavigatorTests
    {
        private HierarchyNavigator _navigator;
        private Mock<AuthClient> _mockClient;
        private TradingParameters _tradingParameters;
        private PhaseParameters _phase1Params;
        private PhaseParameters _phase2Params;
        private Dictionary<int, CustomLayerConfig> _customLayerConfigs;
        private Mock<TokenStorage> _mockStorage;

        [SetUp]
        public void SetUp()
        {
            // Initialize with default values.  We'll modify these in specific tests.
            _tradingParameters = new TradingParameters
            {
                HierarchyLevels = 3,
                MaxHierarchyDepth = 3,
                MaxDrawdown = 1000,
                Barrier = 1.5m,
                MartingaleLevel = 2,
                Stake = 10
            };

            _phase1Params = new PhaseParameters
            {
                MaxDrawdown = 1000,
                Barrier = 1.5m,
                MartingaleLevel = 2
            };

            _phase2Params = new PhaseParameters
            {
                MaxDrawdown = 2000,
                Barrier = 2.0m,
                MartingaleLevel = 3
            };

            _customLayerConfigs = new Dictionary<int, CustomLayerConfig>();

            // Mock TokenStorage.  For now, we're just mocking the necessary methods.
            _mockStorage = new Mock<TokenStorage>(new object[] { "dummy_path.json" });
            _mockStorage.Setup(x => x.customLayerConfigs).Returns(_customLayerConfigs);

            _mockClient = new Mock<AuthClient>(new Credentials { AppId = "51558", Token = "Eb50l6kGVlahrMW" }, 0);
            _mockClient.Setup(x => x.TradingParameters).Returns(_tradingParameters); 
        }


        [Test]
        public void MoveToNextLevel_ShouldMoveToNextLevelInSameLayer()
        {
            // Arrange
            var navigator = new HierarchyNavigator(500, _tradingParameters, _phase1Params, _phase2Params, _customLayerConfigs, 10, _mockStorage.Object);
            navigator.currentLevelId = "1.1";

            // Act
            navigator.MoveToNextLevel(_mockClient.Object);

            // Assert
            Assert.That(navigator.currentLevelId, Is.EqualTo("1.2")); // NUnit 4 syntax
        }

        [Test]
        public void MoveToNextLevel_ShouldMoveToParentLevel_FromLayer2()
        {
            // Arrange
            // Create a navigator that will create a hierarchy (amountToBeRecovered > phase1Params.MaxDrawdown)
            var navigator = new HierarchyNavigator(5000, _tradingParameters, _phase1Params, _phase2Params, _customLayerConfigs, 10, _mockStorage.Object);
            navigator.currentLevelId = "2.1"; // Start on Layer 2, Level 1

            // Act
            navigator.MoveToNextLevel(_mockClient.Object);

            // Assert
            Assert.That(navigator.currentLevelId, Is.EqualTo("1.3")); // Should have moved up to 1.3
        }


        [Test]
        public void MoveToNextLevel_ShouldExitHierarchyMode_FromLayer1LastLevel()
        {
            // Arrange
            // Create a navigator that will create a hierarchy.
            var navigator = new HierarchyNavigator(5000, _tradingParameters, _phase1Params, _phase2Params, _customLayerConfigs, 10, _mockStorage.Object);
            navigator.currentLevelId = "1.3"; // Last level in layer 1 (assuming HierarchyLevels = 3)
            navigator.layer1CompletedLevels = 2; // Set to 2, so the next increment will trigger exit.

            // Act
            navigator.MoveToNextLevel(_mockClient.Object);

            // Assert
            Assert.That(navigator.IsInHierarchyMode, Is.False); // NUnit 4 syntax
            Assert.That(navigator.currentLevelId, Is.EqualTo("0")); // Should be back to root level
        }

        [Test]
        public void MoveToNextLevel_ShouldMoveToNextLevelInLayer1_WhenNotAllLevelsComplete()
        {
            // Arrange
            var navigator = new HierarchyNavigator(5000, _tradingParameters, _phase1Params, _phase2Params, _customLayerConfigs, 10, _mockStorage.Object);
            navigator.currentLevelId = "1.2"; // Not the last level
            navigator.layer1CompletedLevels = 1;

            // Act
            navigator.MoveToNextLevel(_mockClient.Object);

            // Assert
            Assert.That(navigator.currentLevelId, Is.EqualTo("1.3")); // Should move to 1.3
            Assert.That(navigator.IsInHierarchyMode, Is.True); // Should still be in hierarchy mode
        }

        [Test]
        public void MoveToNextLevel_ShouldIncrementLayer1CompletedLevels()
        {
            // Arrange
            var navigator = new HierarchyNavigator(5000, _tradingParameters, _phase1Params, _phase2Params, _customLayerConfigs, 10, _mockStorage.Object);
            navigator.currentLevelId = "1.1";

            // Act
            navigator.MoveToNextLevel(_mockClient.Object); // Move from 1.1 to 1.2

            // Assert
            Assert.That(navigator.layer1CompletedLevels, Is.EqualTo(1)); // layer1CompletedLevels should be 1

            // Act again
            navigator.MoveToNextLevel(_mockClient.Object); // Move from 1.2 to 1.3

            // Assert
            Assert.That(navigator.layer1CompletedLevels, Is.EqualTo(2)); // layer1CompletedLevels should be 2
        }

        [Test]
        public void CreateHierarchy_EntersHierarchyMode()
        {
            // Arrange
            var navigator = new HierarchyNavigator(5000, _tradingParameters, _phase1Params, _phase2Params, _customLayerConfigs, 10, _mockStorage.Object);

            // Assert
            Assert.That(navigator.IsInHierarchyMode, Is.True);
        }

        [Test]
        public void CreateHierarchy_DoesNotEnterHierarchyMode_IfAmountLow()
        {
            // Arrange
            // amountToBeRecovered (500) is less than phase1Params.MaxDrawdown (1000)
            var navigator = new HierarchyNavigator(500, _tradingParameters, _phase1Params, _phase2Params, _customLayerConfigs, 10, _mockStorage.Object);

            // Assert
            Assert.That(navigator.IsInHierarchyMode, Is.False);
        }
    }
}