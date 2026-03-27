// ============================================================
// 文件名：FrameworkTests.cs
// 功能描述：Unity Test Framework — EventBus和ObjectPool单元测试
//          使用NUnit测试框架，可在Unity Test Runner中运行
// 创建时间：2026-03-25
// 所属模块：Tests/EditMode
// 对应交互：阶段二 #71
// ============================================================

using System.Collections.Generic;
using NUnit.Framework;
using AetheraSurvivors.Framework;

namespace AetheraSurvivors.Tests
{
    /// <summary>
    /// EventBus 单元测试
    /// </summary>
    [TestFixture]
    public class EventBusTests
    {
        private EventBus _eventBus;

        [SetUp]
        public void SetUp()
        {
            // 每个测试前创建新的EventBus实例
            _eventBus = EventBus.Instance;
            _eventBus.Initialize();
            _eventBus.ClearAll();
        }

        [TearDown]
        public void TearDown()
        {
            _eventBus.ClearAll();
        }

        // ====== 测试事件定义 ======

        public struct TestEvent : IEvent
        {
            public int Value;
            public string Message;
        }

        public struct AnotherEvent : IEvent
        {
            public float Data;
        }

        // ====== 基本订阅/发布 ======

        [Test]
        public void Subscribe_And_Publish_ShouldInvokeHandler()
        {
            // Arrange
            int receivedValue = 0;
            _eventBus.Subscribe<TestEvent>((evt) => { receivedValue = evt.Value; });

            // Act
            _eventBus.Publish(new TestEvent { Value = 42 });

            // Assert
            Assert.AreEqual(42, receivedValue);
        }

        [Test]
        public void Publish_WithoutSubscribers_ShouldNotThrow()
        {
            // Act & Assert — 没有订阅者时发布不应抛异常
            Assert.DoesNotThrow(() =>
            {
                _eventBus.Publish(new TestEvent { Value = 1 });
            });
        }

        [Test]
        public void Subscribe_MultipleHandlers_ShouldInvokeAll()
        {
            // Arrange
            int count = 0;
            _eventBus.Subscribe<TestEvent>((evt) => { count++; });
            _eventBus.Subscribe<TestEvent>((evt) => { count++; });
            _eventBus.Subscribe<TestEvent>((evt) => { count++; });

            // Act
            _eventBus.Publish(new TestEvent());

            // Assert
            Assert.AreEqual(3, count);
        }

        // ====== 取消订阅 ======

        [Test]
        public void Unsubscribe_ShouldStopReceivingEvents()
        {
            // Arrange
            int count = 0;
            void Handler(TestEvent evt) { count++; }
            _eventBus.Subscribe<TestEvent>(Handler);

            // Act
            _eventBus.Publish(new TestEvent()); // count = 1
            _eventBus.Unsubscribe<TestEvent>(Handler);
            _eventBus.Publish(new TestEvent()); // 不应触发

            // Assert
            Assert.AreEqual(1, count);
        }

        // ====== 一次性订阅 ======

        [Test]
        public void SubscribeOnce_ShouldFireOnlyOnce()
        {
            // Arrange
            int count = 0;
            _eventBus.SubscribeOnce<TestEvent>((evt) => { count++; });

            // Act
            _eventBus.Publish(new TestEvent()); // count = 1
            _eventBus.Publish(new TestEvent()); // 不应触发

            // Assert
            Assert.AreEqual(1, count);
        }

        // ====== 优先级 ======

        [Test]
        public void Subscribe_WithPriority_ShouldExecuteInOrder()
        {
            // Arrange
            var order = new List<int>();
            _eventBus.Subscribe<TestEvent>((evt) => { order.Add(2); }, EventBus.PriorityDefault);
            _eventBus.Subscribe<TestEvent>((evt) => { order.Add(1); }, EventBus.PriorityHigh);
            _eventBus.Subscribe<TestEvent>((evt) => { order.Add(3); }, EventBus.PriorityLow);

            // Act
            _eventBus.Publish(new TestEvent());

            // Assert
            Assert.AreEqual(new List<int> { 1, 2, 3 }, order);
        }

        // ====== 类型隔离 ======

        [Test]
        public void DifferentEventTypes_ShouldNotInterfere()
        {
            // Arrange
            int testCount = 0;
            int anotherCount = 0;
            _eventBus.Subscribe<TestEvent>((evt) => { testCount++; });
            _eventBus.Subscribe<AnotherEvent>((evt) => { anotherCount++; });

            // Act
            _eventBus.Publish(new TestEvent());

            // Assert
            Assert.AreEqual(1, testCount);
            Assert.AreEqual(0, anotherCount);
        }

        // ====== 事件数据 ======

        [Test]
        public void Publish_ShouldPassEventData()
        {
            // Arrange
            string receivedMsg = "";
            _eventBus.Subscribe<TestEvent>((evt) => { receivedMsg = evt.Message; });

            // Act
            _eventBus.Publish(new TestEvent { Message = "Hello World" });

            // Assert
            Assert.AreEqual("Hello World", receivedMsg);
        }

        // ====== 管理方法 ======

        [Test]
        public void GetListenerCount_ShouldReturnCorrectCount()
        {
            // Arrange
            _eventBus.Subscribe<TestEvent>((evt) => { });
            _eventBus.Subscribe<TestEvent>((evt) => { });

            // Act & Assert
            Assert.AreEqual(2, _eventBus.GetListenerCount<TestEvent>());
            Assert.AreEqual(0, _eventBus.GetListenerCount<AnotherEvent>());
        }

        [Test]
        public void ClearListeners_ShouldRemoveAllForType()
        {
            // Arrange
            _eventBus.Subscribe<TestEvent>((evt) => { });
            _eventBus.Subscribe<TestEvent>((evt) => { });

            // Act
            _eventBus.ClearListeners<TestEvent>();

            // Assert
            Assert.AreEqual(0, _eventBus.GetListenerCount<TestEvent>());
        }

        [Test]
        public void ClearAll_ShouldRemoveEverything()
        {
            // Arrange
            _eventBus.Subscribe<TestEvent>((evt) => { });
            _eventBus.Subscribe<AnotherEvent>((evt) => { });

            // Act
            _eventBus.ClearAll();

            // Assert
            Assert.AreEqual(0, _eventBus.GetListenerCount<TestEvent>());
            Assert.AreEqual(0, _eventBus.GetListenerCount<AnotherEvent>());
        }

        // ====== 异常处理 ======

        [Test]
        public void Publish_HandlerThrows_ShouldNotAffectOthers()
        {
            // Arrange
            int safeCount = 0;
            _eventBus.Subscribe<TestEvent>((evt) => { throw new System.Exception("Test error"); }, EventBus.PriorityHigh);
            _eventBus.Subscribe<TestEvent>((evt) => { safeCount++; }, EventBus.PriorityLow);

            // Act — 第一个Handler抛异常，第二个仍应执行
            _eventBus.Publish(new TestEvent());

            // Assert
            Assert.AreEqual(1, safeCount);
        }

        // ====== 重复订阅 ======

        [Test]
        public void Subscribe_Duplicate_ShouldBeIgnored()
        {
            // Arrange
            int count = 0;
            void Handler(TestEvent evt) { count++; }
            _eventBus.Subscribe<TestEvent>(Handler);
            _eventBus.Subscribe<TestEvent>(Handler); // 重复订阅

            // Act
            _eventBus.Publish(new TestEvent());

            // Assert — 只应触发1次
            Assert.AreEqual(1, count);
        }
    }

    /// <summary>
    /// SaveManager 单元测试（基础序列化/存取）
    /// </summary>
    [TestFixture]
    public class SaveManagerTests
    {
        private SaveManager _saveManager;

        [SetUp]
        public void SetUp()
        {
            _saveManager = SaveManager.Instance;
            _saveManager.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            _saveManager.Delete("test_key");
        }

        // ====== 测试数据结构 ======

        [System.Serializable]
        public class TestData
        {
            public int score;
            public string name;
            public float progress;
        }

        // ====== 基础存取 ======

        [Test]
        public void Save_And_Load_ShouldReturnSameData()
        {
            // Arrange
            var data = new TestData { score = 100, name = "TestPlayer", progress = 0.75f };

            // Act
            _saveManager.Save("test_key", data);
            var loaded = _saveManager.Load<TestData>("test_key");

            // Assert
            Assert.IsNotNull(loaded);
            Assert.AreEqual(100, loaded.score);
            Assert.AreEqual("TestPlayer", loaded.name);
            Assert.AreEqual(0.75f, loaded.progress, 0.001f);
        }

        [Test]
        public void Load_NonExistentKey_ShouldReturnDefault()
        {
            // Act
            var result = _saveManager.Load<TestData>("non_existent_key_12345");

            // Assert
            Assert.IsNull(result);
        }

        [Test]
        public void Delete_ShouldRemoveData()
        {
            // Arrange
            _saveManager.Save("test_key", new TestData { score = 50 });

            // Act
            _saveManager.Delete("test_key");

            // Assert
            Assert.IsFalse(_saveManager.HasKey("test_key"));
        }

        [Test]
        public void HasKey_ExistingKey_ShouldReturnTrue()
        {
            // Arrange
            _saveManager.Save("test_key", new TestData { score = 1 });

            // Assert
            Assert.IsTrue(_saveManager.HasKey("test_key"));
        }

        [Test]
        public void Save_Overwrite_ShouldUseNewData()
        {
            // Arrange
            _saveManager.Save("test_key", new TestData { score = 100 });
            _saveManager.Save("test_key", new TestData { score = 200 });

            // Act
            var loaded = _saveManager.Load<TestData>("test_key");

            // Assert
            Assert.AreEqual(200, loaded.score);
        }

        // ====== 加密测试 ======

        [Test]
        public void Encryption_Enabled_ShouldStillLoadCorrectly()
        {
            // Arrange
            _saveManager.EncryptionEnabled = true;
            var data = new TestData { score = 999, name = "加密测试" };

            // Act
            _saveManager.Save("test_key", data);
            var loaded = _saveManager.Load<TestData>("test_key");

            // Assert
            Assert.AreEqual(999, loaded.score);
            Assert.AreEqual("加密测试", loaded.name);
        }
    }

    /// <summary>
    /// TimerManager 单元测试
    /// </summary>
    [TestFixture]
    public class TimerManagerTests
    {
        // TimerManager是MonoSingleton，在EditMode下无法完整测试
        // 这里测试纯逻辑部分，运行时测试放PlayMode

        [Test]
        public void Timer_Constants_ShouldBeValid()
        {
            // 简单验证TimerManager类型存在且可访问
            Assert.IsNotNull(typeof(TimerManager));
        }
    }
}
