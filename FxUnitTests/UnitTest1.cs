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
        private Mock<TokenStorage> _mockStorage; // Mock TokenStorage

        [SetUp]
        public void SetUp()
        {
            // Initialize with default values.  We'll modify these in specific tests.
            _tradingParameters = new TradingParameters
            {
                HierarchyLevels = 3,
                MaxHierarchyDepth = 3, // Add MaxHierarchyDepth
                MaxDrawdown = 1000,
                Barrier = 1.5m,
                MartingaleLevel = 2,
                Stake = 10 // Add initial stake
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
            _mockStorage = new Mock<TokenStorage>(new object[] { "dummy_path.json" }); // Pass a dummy path
            _mockStorage.Setup(x => x.customLayerConfigs).Returns(_customLayerConfigs);

            _mockClient = new Mock<AuthClient>(new Credentials { AppId = "123", Token = "test_token" }, 0); // Pass dummy credentials
            _mockClient.Setup(x => x.TradingParameters).Returns(_tradingParameters); // Return _tradingParameters

            // Initialize _navigator here, so it's available for all tests.
            _navigator = new HierarchyNavigator(5000, _tradingParameters, _phase1Params, _phase2Params, _customLayerConfigs, 10, _mockStorage.Object);

        }


        [Test]
        public void MoveToNextLevel_ShouldMoveToNextLevelInSameLayer()
        {
            // Arrange
            _navigator.currentLevelId = "1.1";

            // Act
            _navigator.MoveToNextLevel(_mockClient.Object);

            // Assert
            Assert.That(_navigator.currentLevelId, Is.EqualTo("1.2"));
        }

        [Test]
        public void MoveToNextLevel_ShouldMoveToParentLevel_FromLayer2()
        {
            // Arrange
            _navigator.currentLevelId = "2.3"; // Start on Layer 2, last Level

            // Act
            _navigator.MoveToNextLevel(_mockClient.Object);

            // Assert
            Assert.That(_navigator.currentLevelId, Is.EqualTo("1.3")); // Should have moved up to 1.3
        }


        [Test]
        public void MoveToNextLevel_ShouldExitHierarchyMode_FromLayer1LastLevel()
        {
            // Arrange
            _navigator.currentLevelId = "1.3"; // Last level in layer 1 (assuming HierarchyLevels = 3)
            _navigator.layer1CompletedLevels = 2; // Set to 2, so the next increment will trigger exit.

            // Act
            _navigator.MoveToNextLevel(_mockClient.Object);

            // Assert
            Assert.That(_navigator.IsInHierarchyMode, Is.False);
            Assert.That(_navigator.currentLevelId, Is.EqualTo("0")); // Should be back to root level
        }

        [Test]
        public void MoveToNextLevel_ShouldMoveToNextLevelInLayer1_WhenNotAllLevelsComplete()
        {
            // Arrange
            _navigator.currentLevelId = "1.2"; // Not the last level
            // layer1CompletedLevels is initialized to 0

            // Act
            _navigator.MoveToNextLevel(_mockClient.Object);

            // Assert
            Assert.That(_navigator.currentLevelId, Is.EqualTo("1.3")); // Should move to 1.3
            Assert.That(_navigator.IsInHierarchyMode, Is.True); // Should still be in hierarchy mode
        }

        [Test]
        public void MoveToNextLevel_ShouldIncrementLayer1CompletedLevels()
        {
            // Arrange
            _navigator.currentLevelId = "1.1";

            // Act
            _navigator.MoveToNextLevel(_mockClient.Object); // Move from 1.1 to 1.2

            // Assert
            Assert.That(_navigator.layer1CompletedLevels, Is.EqualTo(1)); // layer1CompletedLevels should be 1

            // Act again
            _navigator.MoveToNextLevel(_mockClient.Object); // Move from 1.2 to 1.3

            // Assert
            Assert.That(_navigator.layer1CompletedLevels, Is.EqualTo(2)); // layer1CompletedLevels should be 2
        }

        [Test]
        public void CreateHierarchy_EntersHierarchyMode()
        {
            // Arrange is already handled in setup

            // Assert
            Assert.That(_navigator.IsInHierarchyMode, Is.True);
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

        [Test]
        public void MoveToNextLevel_ShouldMoveToParentLevel_FromNestedLayer()
        {
            // Arrange
            _navigator.currentLevelId = "1.2.3.1"; // Nested level

            // Act
            _navigator.MoveToNextLevel(_mockClient.Object);

            // Assert
            Assert.That(_navigator.currentLevelId, Is.EqualTo("1.2.3")); // Should move up to parent
        }

        [Test]
        public void MoveToNextLevel_ShouldMoveToParentLevel_FromNestedLayer2()
        {
            // Arrange
            _navigator.currentLevelId = "1.2.3.3"; // Nested level

            // Act
            _navigator.MoveToNextLevel(_mockClient.Object);

            // Assert
            Assert.That(_navigator.currentLevelId, Is.EqualTo("1.2.3")); // Should move up to parent
        }
    }
}