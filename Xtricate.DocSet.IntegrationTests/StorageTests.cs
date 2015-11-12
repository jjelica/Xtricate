﻿using System;
using System.Collections.Generic;
using System.Data.Entity.Infrastructure;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using Ploeh.AutoFixture;
using StackExchange.Profiling;
using Xtricate.DocSet.IntegrationTests.Profiling;

namespace Xtricate.DocSet.IntegrationTests
{
    public class SerializerTests
    {
        [TestFixtureSetUp]
        public void Setup()
        {
            MiniProfiler.Settings.Storage = new MiniPofilerInMemoryStorage();
            MiniProfiler.Settings.ProfilerProvider = new MiniPofilerInMemoryProvider();
        }

        [TestCase(1, false)]
        [TestCase(1, true)]
        [TestCase(2, true)]
        public void JsonNetPerformanceTests(int docCount, bool warmup)
        {
            Trace.WriteLine(string.Format("JsonNetPerformanceTests: warmup={0}, count={1}", docCount, docCount));
            var docs = new Fixture().CreateMany<TestDocument>(docCount).ToList();
            MiniProfiler.Start();
            var mp = MiniProfiler.Current;
            Trace.WriteLine("performance test on: " + docs.Count() + " docs");

            var jilSserializer = new JilSerializer();
            Trace.WriteLine("start JIL");
            if(warmup) jilSserializer.ToJson(new Fixture().Create<TestDocument>()); // warmup
            using (mp.Step("JIL serialization"))
            {
                1.Times(i =>
                {
                    foreach (var doc in docs)
                        jilSserializer.ToJson(doc);
                });
            }

            var jsonNetSerializer = new JsonNetSerializer();
            Trace.WriteLine("start JSONNET");
            if (warmup) jsonNetSerializer.ToJson(new Fixture().Create<TestDocument>()); // warmup
            using (mp.Step("JSONNET serialization"))
            {
                1.Times(i =>
                {
                    foreach (var doc in docs)
                        jsonNetSerializer.ToJson(doc);
                });
            }

            var textSerializer = new ServiceStackTextSerializer();
            Trace.WriteLine("start JSONNET");
            if (warmup) textSerializer.ToJson(new Fixture().Create<TestDocument>()); // warmup
            using (mp.Step("SERVICESTACK serialization"))
            {
                1.Times(i =>
                {
                    foreach (var doc in docs)
                        textSerializer.ToJson(doc);
                });
            }

            Trace.WriteLine($"trace: {mp.RenderPlainText()}");
            MiniProfiler.Stop();
        }
    }
    [TestFixture]
    public class StorageTests
    {
        [TestFixtureSetUp]
        public void Setup()
        {
            MiniProfiler.Settings.Storage = new MiniPofilerInMemoryStorage();
            MiniProfiler.Settings.ProfilerProvider = new MiniPofilerInMemoryProvider();
        }

        [Test]
        public void UpsertTest()
        {
            var options = new StorageOptions("TestDb", "StorageTests");
            var connectionFactory = new SqlConnectionFactory();
            var indexMap = TestDocumentIndexMap;
            var storage = new DocStorage<TestDocument>(connectionFactory, options, new SqlBuilder(),
                new JsonNetSerializer(), new Md5Hasher(), indexMap);

            MiniProfiler.Start();
            var mp = MiniProfiler.Current;

            storage.Reset();
            var preCount = storage.Count(new[] {"en-US"});
            Trace.WriteLine($"pre count: {preCount}");

            var id = DateTime.Now.Epoch() + new Random().Next(10000, 99999);
            for (var i = 1; i < 100; i++)
            {
                Trace.WriteLine($"+{i}");
                using (mp.Step("upsert " + i))
                {
                    var result1 = storage.Upsert("key1", new Fixture().Create<TestDocument>(), new[] {"en-US"});
                //    Assert.That(result1, Is.EqualTo(StorageAction.Updated));
                //}
                //using (mp.Step("upsert string"))
                //{
                    var result2 = storage.Upsert(Guid.NewGuid(), new Fixture().Create<TestDocument>(), new[] {"en-US"});
                    //Assert.That(result2, Is.EqualTo(StorageAction.Inserted));
                //}
                //using (mp.Step("upsert int"))
                //{
                    var result3 = storage.Upsert(id + i, new Fixture().Create<TestDocument>(), new[] {"en-US"});
                    //Assert.That(result3, Is.EqualTo(StorageAction.Inserted));
                }
            }

            for (var i = 1; i <= 5; i++)
            {
                using (mp.Step("load " + i))
                {
                    var result = storage.Load(new[] {"en-US"}).Take(100);
                    //Assert.That(result, Is.Not.Null);
                    //Assert.That(result, Is.Not.Empty);
                    Trace.WriteLine($"loaded count: {result.Count()}");
                    Trace.WriteLine($"first: {result.FirstOrDefault().Id}");
                    //result.ForEach(r => Trace.Write(r.Id));
                    //result.ForEach(r => Assert.That(r, Is.Not.Null));
                    result.ForEach(r => Trace.WriteLine(r, r.Name));
                }
            }

            using (mp.Step("post count"))
            {
                var postCount = storage.Count(new[] {"en-US"});
                Trace.WriteLine($"post count: {postCount}");
                //Assert.That(storage.Count(), Is.GreaterThan(preCount));
            }
            Trace.WriteLine($"trace: {mp.RenderPlainText()}");
            MiniProfiler.Stop();
        }

        [Test]
        public void InitializeTest()
        {
            var options = new StorageOptions("TestDb", "StorageTests");
            var connectionFactory = new SqlConnectionFactory();
            var indexMap = TestDocumentIndexMap;
            var storage = new DocStorage<TestDocument>(connectionFactory, options, new SqlBuilder(),
                new JsonNetSerializer(), new Md5Hasher(), indexMap);

            storage.Initialize();
            storage.Reset();

            Assert.That(storage.Count(), Is.EqualTo(0));
        }

        [Test]
        public void DeleteTest()
        {
            var options = new StorageOptions("TestDb", "StorageTests");
            var connectionFactory = new SqlConnectionFactory();
            var indexMap = TestDocumentIndexMap;
            var storage = new DocStorage<TestDocument>(connectionFactory, options, new SqlBuilder(),
                new JsonNetSerializer(), new Md5Hasher(), indexMap);

            storage.Initialize();
            storage.Reset();

            Assert.That(storage.Count(), Is.EqualTo(0));

            var doc1 = new Fixture().Create<TestDocument>();
            storage.Upsert("key1", doc1, new[] {"en-US"});

            var doc2 = new Fixture().Create<TestDocument>();
            storage.Upsert("key2", doc1, new[] {"en-US"});

            var doc3 = new Fixture().Create<TestDocument>();
            storage.Upsert("key1", doc1, new[] {"en-GB"});

            Assert.That(storage.Count(), Is.EqualTo(3));

            var result1 =storage.Delete("key1", new[] { "en-US" }); // removes only key1 + en-US

            Assert.That(result1, Is.EqualTo(StorageAction.Deleted));
            Assert.That(storage.Count(), Is.EqualTo(2));

            var result2 = storage.Delete("key1"); // removes all with key1

            Assert.That(result2, Is.EqualTo(StorageAction.Deleted));
            Assert.That(storage.Count(), Is.EqualTo(1));

            var result3 = storage.Delete("key2"); // removes all with key2

            Assert.That(result3, Is.EqualTo(StorageAction.Deleted));
            Assert.That(storage.Count(), Is.EqualTo(0));
        }

        private static List<IIndexMap<TestDocument>> TestDocumentIndexMap
        {
            get
            {
                return new List<IIndexMap<TestDocument>>
                {
                    new IndexMap<TestDocument>(nameof(TestDocument.Name), i => i.Name),
                    new IndexMap<TestDocument>(nameof(TestDocument.Group), i => i.Group),
                    new IndexMap<TestDocument>(nameof(TestSku.Sku), values: i => i.Skus.Select(s => s.Sku)),
                    new IndexMap<TestDocument>(nameof(TestDocument.Date), i =>
                        i.Date.HasValue ? i.Date.Value.ToString("s") : null)
                };
            }
        }
    }

    public class TestDocument
    {
        public int Id { get; set; }
        public IDictionary<string, string> Identifiers { get; set; }
        public string Name { get; set; }
        public string ShortDescription { get; set; }
        public string LongDescription { get; set; }
        public string Group { get; set; }
        public int Position { get; set; }
        public IEnumerable<string> MetaKeywords { get; set; }
        public string MetaDescription { get; set; }
        public TestEnum State { get; set; }
        public DateTime? Date { get; set; }
        public IEnumerable<TestSku> Skus { get; set; }
        public IEnumerable<TestAttributeValue> Features { get; set; }
        public IEnumerable<TestAttributeValue> Relations { get; set; }
        public IEnumerable<TestAttributeValue> Includes { get; set; }
        public IEnumerable<TestAttributeValue> Attributes { get; set; }
    }

    public class TestSku
    {
        public string Sku { get; set; }
        public string Cso { get; set; }
        public string Gtin { get; set; }
        public string Ean { get; set; }
        public string Upc { get; set; }
    }

    public class TestAttributeValue
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Position { get; set; }
        public string TextValue { get; set; }
        public int? MediaValue { get; set; }
        public decimal? NumberValue { get; set; }
        public bool? BooleanValue { get; set; }
        public int? CategoryValue { get; set; }
        public int? ProductValue { get; set; }
        public DateTime? DateValue { get; set; }
    }

    public enum TestEnum
    {
        Open,
        Closed
    }
}