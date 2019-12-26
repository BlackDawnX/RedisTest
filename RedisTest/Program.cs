using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Newtonsoft.Json;
using StackExchange.Redis;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace RedisTest
{
    class StudentModel
    {
        public string stuName { get; set; }
        public int stuAge { get; set; }
        public int stuSex { get; set; }
    }
    class Program
    {
        static ConfigurationOptions configuration;
        static ConnectionMultiplexer redisConn;
        static void Main(string[] args)
        {
            IConfiguration config = new ConfigurationBuilder()
                .Add(new JsonConfigurationSource { Path = "appsettings.json", ReloadOnChange = true })
                .Build();
            configuration = ConfigurationOptions.Parse(config["ip"]);
            configuration.Password = config["pwd"];
            redisConn = ConnectionMultiplexer.Connect(configuration);
            var database = redisConn.GetDatabase();//指定连接到的库
            //SubscriberTest(database);
            SortedSetTest(database);
            Console.Read();
        }
        /// <summary>
        ///  特点:无序排列，值不可重复。增加删除查询都很快。提供了取并集交集差集等一些有用的操作
        /// </summary>
        /// <param name="database"></param>
        static void SetTest(IDatabase database)
        {
            database.KeyDelete("文章1");
            database.KeyDelete("文章2");

            for (int i = 0; i < 3; i++){
                database.SetAdd("文章1", $"用户{i}");
            }
            for (int i = 2; i < 5; i++){
                database.SetAdd("文章2", $"用户{i}");
            }
            Console.WriteLine(database.SetContains("文章1", "用户0"));//true
            Console.WriteLine(database.SetContains("文章1", "用户10"));//false
            Console.WriteLine(string.Join(",", database.SetMembers("文章1", 0).ToArray()));//户2,用户1,用户0
            Console.WriteLine(string.Join(",", database.SetMembers("文章2", 0).ToArray()));//用户3,用户4,用户2
            RedisValue[] inter = database.SetCombine(SetOperation.Intersect, "文章1", "文章2");
            RedisValue[] union = database.SetCombine(SetOperation.Union, "文章1", "文章2");
            RedisValue[] dif1 = database.SetCombine(SetOperation.Difference, "文章1", "文章2");
            RedisValue[] dif2 = database.SetCombine(SetOperation.Difference, "文章2", "文章1");
            Console.WriteLine("两篇文章都评论过的用户");
            Console.WriteLine(string.Join(",", inter.OrderBy(m => m).ToList()));//用户2
            Console.WriteLine("评论过两篇文章中任意一篇文章的用户");
            Console.WriteLine(string.Join(",", union.OrderBy(m => m).ToList()));//用户0,用户1,用户2,用户3,用户4
            Console.WriteLine("只评论过其第一篇文章的用户");
            Console.WriteLine(string.Join(",", dif1.OrderBy(m => m).ToList()));//用户0,用户1
            Console.WriteLine("只评论过其第二篇文章的用户");
            Console.WriteLine(string.Join(",", dif2.OrderBy(m => m).ToList()));//用户3,用户4
        }
        /// <summary>
        /// 特点:有序排列，值不可重复。类似Set，不同的是sortedset的每个元素都会关联一个double类型的score，用此元素来进行排序
        /// </summary>
        /// <param name="database"></param>
        static void SortedSetTest(IDatabase database)
        {
            database.KeyDelete("文章1");
            var setList = new List<SortedSetEntry>();
            for (int i = 1; i <= 5; i++)
            {
                setList.Add(new SortedSetEntry($"用户{i}", 1));
            }
            database.SortedSetAdd("文章1", setList.ToArray());
            Console.WriteLine(string.Join(",", database.SortedSetRangeByRank("文章1")));//用户1,用户2,用户3,用户4,用户5
            database.SortedSetIncrement("文章1", "用户1", 2);//设置分数为2
            database.SortedSetIncrement("文章1", "用户2", 4);//设置分数为4
            Console.WriteLine(string.Join(",", database.SortedSetRangeByRank("文章1", order:Order.Descending)));//用户2,用户1,用户5,用户4,用户3
            Console.WriteLine(string.Join(",", database.SortedSetRangeByRankWithScores("文章1", order: Order.Descending)));//用户2: 5,用户1: 3,用户5: 1,用户4: 1,用户3: 1
            Console.WriteLine(string.Join(",", database.SortedSetRangeByScore("文章1", start:0,stop:3, order: Order.Descending)));//用户1,用户5,用户4,用户3
        }
        public static void HashTest(IDatabase database)
        {
            database.HashSet("student1", "name", "张三");
            database.HashSet("student1", "age", 12);
            database.HashSet("student1", "class", "五年级");
            Console.WriteLine(database.HashGet("student1", "name"));
            RedisValue[] result = database.HashGet("student1", new RedisValue[] { "name", "age", "class" });
            Console.WriteLine(string.Join(",", result));
            database.KeyDelete("student1");
            Console.ReadLine();
        }
        static void ListTest(IDatabase database)
        {
            database.KeyDelete("key1");
            List<RedisValue> list = new List<RedisValue>() 
            {
                "value","value1","value2"
            };
            database.ListRightPush("key1", list.ToArray());
            database.ListLeftPush("key1", list.ToArray());
            Console.WriteLine(database.ListLeftPop("key1"));
            Console.WriteLine(database.ListLeftPop("key1"));
            database.ListTrim("key1",0,1);

            var readList = database.ListRange("key1", 0);
            Console.WriteLine(string.Join(",", readList.ToArray()));
        }

        static void JsonTest(IDatabase database)
        {
            StudentModel studentModel = new StudentModel()
            {
                stuName = "张三",
                stuAge = 18,
                stuSex = 0
            };
            string studentJson = JsonConvert.SerializeObject(studentModel);
            database.StringSet(studentModel.stuName, studentJson);

            StudentModel readStudentModel = JsonConvert.DeserializeObject<StudentModel>(database.StringGet(studentModel.stuName));
            Console.WriteLine($"stuName:{readStudentModel.stuName }\t stuAge:{readStudentModel.stuAge}\t stuSex:{readStudentModel.stuSex}");
        }

        static async void StringIncrementTestAsync(IDatabase database)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            long increment = 0;
            for (int i = 0; i < 100; i++)
            {
                increment=await database.StringIncrementAsync("StringIncrement", 2, flags: CommandFlags.FireAndForget);
            }
            for (int i = 0; i < 190; i++)
            {
                increment = await database.StringDecrementAsync("StringIncrement",1, flags: CommandFlags.FireAndForget);
            }
            stopwatch.Stop();
            Console.WriteLine($"运行时间{stopwatch.ElapsedMilliseconds}");
            Console.WriteLine(increment);
        }
        /// <summary>
        /// 使用Redis发布/订阅
        /// </summary>
        /// <param name="database"></param>
        static void SubscriberTest(IDatabase database)
        {
            ISubscriber sub = redisConn.GetSubscriber();
            sub.Subscribe("messages").OnMessage(channelMessage => 
            {
                Console.WriteLine((string)channelMessage.Message);
            });
            sub.Subscribe("messages").OnMessage(async channelMessage => 
            {
                await Task.Delay(1000);
                Console.WriteLine((string)channelMessage.Message);
            });

            Console.WriteLine(sub.Publish("messages", "hello"));
        }
    }
}
