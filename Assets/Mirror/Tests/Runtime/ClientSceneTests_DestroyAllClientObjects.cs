using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests.Runtime.ClientSceneTests
{
    public class TestListenerBehaviour : MonoBehaviour
    {
        // If object is destroyed then both OnDisable and OnDestroy will be called
        public event Action onDestroyCalled;
        public event Action onDisableCalled;

        public void OnDisable() => onDisableCalled?.Invoke();
        void OnDestroy() => onDestroyCalled?.Invoke();
    }

    // A network Behaviour that changes NetworkIdentity.spawned in OnDisable
    public class BadBehaviour : MonoBehaviour
    {
        public void OnDisable()
        {
            GameObject go = new GameObject();
            NetworkIdentity netId = go.AddComponent<NetworkIdentity>();
            const int id = 32032;
            netId.netId = id;

            NetworkIdentity.spawned.Add(id, netId);
        }
    }

    public class ClientSceneTests_DestroyAllClientObjects : MirrorPlayModeTest
    {
        Dictionary<uint, NetworkIdentity> spawned => NetworkIdentity.spawned;
        Dictionary<Guid, UnSpawnDelegate> unspawnHandlers => NetworkClient.unspawnHandlers;

        [UnityTearDown]
        public override IEnumerator UnityTearDown()
        {
            spawned.Clear();
            unspawnHandlers.Clear();
            base.TearDown();
            yield return null;
        }

        TestListenerBehaviour CreateAndAddObject(uint netId, ulong sceneId)
        {
            CreateNetworked(out GameObject go, out NetworkIdentity identity);
            identity.netId = netId;
            identity.sceneId = sceneId;
            TestListenerBehaviour listener = go.AddComponent<TestListenerBehaviour>();
            spawned.Add(netId, identity);
            return listener;
        }

        [UnityTest]
        public IEnumerator DestroysAllNetworkPrefabsInScene()
        {
            // sceneId 0 is prefab
            TestListenerBehaviour listener1 = CreateAndAddObject(10001, 0);
            TestListenerBehaviour listener2 = CreateAndAddObject(10002, 0);

            int destroyCalled1 = 0;
            int destroyCalled2 = 0;

            listener1.onDestroyCalled += () => destroyCalled1++;
            listener2.onDestroyCalled += () => destroyCalled2++;

            NetworkClient.DestroyAllClientObjects();

            // wait for frame to make sure unity events are called
            yield return null;

            Assert.That(destroyCalled1, Is.EqualTo(1));
            Assert.That(destroyCalled2, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator DisablesAllNetworkSceneObjectsInScene()
        {
            // sceneId 0 is prefab
            TestListenerBehaviour listener1 = CreateAndAddObject(20001, 101);
            TestListenerBehaviour listener2 = CreateAndAddObject(20002, 102);

            int disableCalled1 = 0;
            int disableCalled2 = 0;

            listener1.onDisableCalled += () => disableCalled1++;
            listener2.onDisableCalled += () => disableCalled2++;

            int destroyCalled1 = 0;
            int destroyCalled2 = 0;

            listener1.onDestroyCalled += () => destroyCalled1++;
            listener2.onDestroyCalled += () => destroyCalled2++;

            NetworkClient.DestroyAllClientObjects();

            // wait for frame to make sure unity events are called
            yield return null;

            Assert.That(disableCalled1, Is.EqualTo(1));
            Assert.That(disableCalled2, Is.EqualTo(1));

            Assert.That(destroyCalled1, Is.EqualTo(0), "Scene objects should not be destroyed");
            Assert.That(destroyCalled2, Is.EqualTo(0), "Scene objects should not be destroyed");
        }

        [Test]
        public void CallsUnspawnHandlerInsteadOfDestroy()
        {
            // sceneId 0 is prefab
            TestListenerBehaviour listener1 = CreateAndAddObject(30001, 0);
            TestListenerBehaviour listener2 = CreateAndAddObject(30002, 0);

            Guid guid1 = Guid.NewGuid();
            Guid guid2 = Guid.NewGuid();

            int unspawnCalled1 = 0;
            int unspawnCalled2 = 0;

            unspawnHandlers.Add(guid1, x => unspawnCalled1++);
            unspawnHandlers.Add(guid2, x => unspawnCalled2++);
            listener1.GetComponent<NetworkIdentity>().assetId = guid1;
            listener2.GetComponent<NetworkIdentity>().assetId = guid2;

            int disableCalled1 = 0;
            int disableCalled2 = 0;

            listener1.onDisableCalled += () => disableCalled1++;
            listener2.onDisableCalled += () => disableCalled2++;

            NetworkClient.DestroyAllClientObjects();

            Assert.That(unspawnCalled1, Is.EqualTo(1));
            Assert.That(unspawnCalled2, Is.EqualTo(1));

            Assert.That(disableCalled1, Is.EqualTo(0), "Object with UnspawnHandler should not be destroyed");
            Assert.That(disableCalled2, Is.EqualTo(0), "Object with UnspawnHandler should not be destroyed");
        }

        [Test]
        public void ClearsSpawnedList()
        {
            // sceneId 0 is prefab
            TestListenerBehaviour listener1 = CreateAndAddObject(30001, 0);
            TestListenerBehaviour listener2 = CreateAndAddObject(30002, 0);

            NetworkClient.DestroyAllClientObjects();

            Assert.That(spawned, Is.Empty);
        }

        [Test]
        public void CatchesAndLogsExeptionWhenSpawnedListIsChanged()
        {
            GameObject badGameObject = new GameObject("bad", typeof(NetworkIdentity), typeof(BadBehaviour));
            NetworkIdentity netId = badGameObject.GetComponent<NetworkIdentity>();
            const int id = 3535;
            netId.netId = id;
            spawned.Add(id, netId);

            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException"));
            LogAssert.Expect(LogType.Error, "Could not DestroyAllClientObjects because spawned list was modified during loop, make sure you are not modifying NetworkIdentity.spawned by calling NetworkServer.Destroy or NetworkServer.Spawn in OnDestroy or OnDisable.");
            NetworkClient.DestroyAllClientObjects();
        }
    }
}
