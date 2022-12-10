using Extensions;

namespace DataTestApp
{
    public class Tests
    {
        private string alphabet;
        private string[] hosts;
        private string[] domains;
        private string[] ports;
        private string[] categories;
        private Random random;
        private List<UserModel> users;

        [SetUp]
        public async Task Setup()
        {
            alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
            hosts = new string[] { "http://", "https://" };
            domains = new string[] { ".ru", ".com", ".bet" };
            ports = new string[] { ":2096", ":2095", ":2083", ":3082", ":2086", ":2087", ":4095", ":1083", ":2082", ":2086", ":2087" };
            categories = new string[] { "Cpanel", "Whm" };
            random = new();
            users = await UsersController.GetUsersAsync();
        }

        private string GetRandomUrl(int len = 10)
        {
            string url = string.Empty;
            url += hosts[random.Next(hosts.Length)];
            for (int i = 0; i < len; i++)
            {
                url += alphabet[random.Next(alphabet.Length)];
            }
            url += domains[random.Next(domains.Length)];
            url += ports[random.Next(ports.Length)];
            return url;
        }

        private string GetRandomLoginOrPassword(int len = 10)
        {
            string result = string.Empty;
            for (int i = 0; i < len; i++)
            {
                result += alphabet[random.Next(alphabet.Length)];
            }
            return result;
        }

        private DublicateModel MakeRandomLogModel()
        {
            UserModel randomUser = users.GetRandom();
            return new()
            {
                Url = GetRandomUrl(),
                Login = GetRandomLoginOrPassword(),
                Password = GetRandomLoginOrPassword(),
                UploadedByUserId = randomUser.Id,
                UploadedByUsername = randomUser.Username,
                Category = categories.GetRandom()
            };
        }

        [Test]
        public async Task TestLoad100kLogs()
        {
            List<DublicateModel> logs = await DublicatesController.GetLogsAsync();
            Assert.That(logs, Has.Count.EqualTo(100000));
        }

        [Test]
        public async Task TestFillRandomLogsToDb()
        {
            for (int i = 0; i < 10000; i++)
            {
                await DublicatesController.PostLogAsync(MakeRandomLogModel());
            }
            List<DublicateModel> logs = await DublicatesController.GetLogsAsync();
            Assert.That(logs, Has.Count.EqualTo(10000));
        }

        [Test]
        public async Task TestFillRandomLogsToDbWithParallel()
        {
            List<Task> waitTasks = new();
            for (int i = 0; i < 100; i++)
            {
                waitTasks.Add(Task.Run(async () =>
                {
                    for (int i = 0; i < 10000; i++)
                    {
                        await DublicatesController.PostLogAsync(MakeRandomLogModel());
                    }
                }));
            }
            await Task.WhenAll(waitTasks);
            List<DublicateModel> logs = await DublicatesController.GetLogsAsync();
            Assert.That(logs, Has.Count.EqualTo(1000000));
        }

        [Test]
        public async Task TestFindThousandLogsInDb()
        {
            List<DublicateModel> logs = await DublicatesController.GetLogsAsync();
            logs = logs.OrderBy(l => l.Login).ToList();

            List<DublicateModel> takedLogs = logs.Take(100).ToList();

            List<double> timeEllapsedListV1 = new();
            List<double> timeEllapsedListV2 = new();

            Task v1Task = Task.Run(async () =>
            {
                foreach (var log in takedLogs)
                {
                    Stopwatch watcher = new();
                    watcher.Start();
                    bool isLogExist = await DublicatesController.IsLogExist(log.ToString());
                    watcher.Stop();
                    timeEllapsedListV1.Add(watcher.Elapsed.TotalSeconds);
                    Assert.That(isLogExist, Is.True);
                }
            });
            Task v2Task = Task.Run(() =>
            {
                foreach (var log in takedLogs)
                {
                    Stopwatch watcher = new();
                    watcher.Start();
                    bool isLogExist = DublicatesController.IsLogExistV2(log.ToString());
                    watcher.Stop();
                    timeEllapsedListV2.Add(watcher.Elapsed.TotalSeconds);
                    Assert.That(isLogExist, Is.True);
                }
            });
            await Task.WhenAll(v1Task, v2Task);

            Console.WriteLine("V1 results:");
            Console.WriteLine($"V1-Min time for search: {timeEllapsedListV1.Min()} sec.");
            Console.WriteLine($"V1-Max time for search: {timeEllapsedListV1.Max()} sec.");
            Console.WriteLine($"V1-Avg time for search: {timeEllapsedListV1.Average()} sec.");

            Console.WriteLine("V2 results:");
            Console.WriteLine($"V2-Min time for search: {timeEllapsedListV2.Min()} sec.");
            Console.WriteLine($"V2-Max time for search: {timeEllapsedListV2.Max()} sec.");
            Console.WriteLine($"V2-Avg time for search: {timeEllapsedListV2.Average()} sec.");
        }

        [Test]
        public void TestPartitionsExtension()
        {
            string[] words = new string[] { "abc", "efg", "uuu", "lol", "tet" };

            List<string> ex1 = new();
            List<string> ex2 = new();
            List<string> ex3 = new();
            List<string> ex4 = new();
            List<string> ex5 = new();

            for (int i = 0; i < random.Next(1000, 9999); i++) ex1.Add(words[random.Next(words.Length)]);
            for (int i = 0; i < random.Next(1000, 9999); i++) ex2.Add(words[random.Next(words.Length)]);
            for (int i = 0; i < random.Next(1000, 9999); i++) ex3.Add(words[random.Next(words.Length)]);
            for (int i = 0; i < random.Next(1000, 9999); i++) ex4.Add(words[random.Next(words.Length)]);
            for (int i = 0; i < random.Next(1000, 9999); i++) ex5.Add(words[random.Next(words.Length)]);

            Console.WriteLine($"ex 1 size: {ex1.Count}");
            Console.WriteLine($"ex 2 size: {ex2.Count}");
            Console.WriteLine($"ex 3 size: {ex3.Count}");
            Console.WriteLine($"ex 4 size: {ex4.Count}");
            Console.WriteLine($"ex 5 size: {ex5.Count}");

            var partitions1 = ex1.Partitions(10);
            var partitions2 = ex2.Partitions(10);
            var partitions3 = ex3.Partitions(10);
            var partitions4 = ex4.Partitions(10);
            var partitions5 = ex5.Partitions(10);

            Console.WriteLine($"partitions 1 count: {partitions1.Count}");
            Console.WriteLine($"partitions 2 count: {partitions2.Count}");
            Console.WriteLine($"partitions 3 count: {partitions3.Count}");
            Console.WriteLine($"partitions 4 count: {partitions4.Count}");
            Console.WriteLine($"partitions 5 count: {partitions5.Count}");

            string partition1count = string.Empty;
            string partition2count = string.Empty;
            string partition3count = string.Empty;
            string partition4count = string.Empty;
            string partition5count = string.Empty;

            for (int i = 0; i < partitions1.Count; i++) partition1count += partitions1[i].Count + " ";
            for (int i = 0; i < partitions2.Count; i++) partition2count += partitions1[i].Count + " ";
            for (int i = 0; i < partitions3.Count; i++) partition3count += partitions1[i].Count + " ";
            for (int i = 0; i < partitions4.Count; i++) partition4count += partitions1[i].Count + " ";
            for (int i = 0; i < partitions5.Count; i++) partition5count += partitions1[i].Count + " ";

            Console.WriteLine($"partitions 1 count of any: {partition1count}");
            Console.WriteLine($"partitions 2 count of any: {partition2count}");
            Console.WriteLine($"partitions 3 count of any: {partition3count}");
            Console.WriteLine($"partitions 4 count of any: {partition4count}");
            Console.WriteLine($"partitions 5 count of any: {partition5count}");

            int partition1total = 0;
            int partition2total = 0;
            int partition3total = 0;
            int partition4total = 0;
            int partition5total = 0;

            for (int i = 0; i < partitions1.Count; i++) partition1total += partitions1[i].Count;
            for (int i = 0; i < partitions2.Count; i++) partition2total += partitions2[i].Count;
            for (int i = 0; i < partitions3.Count; i++) partition3total += partitions3[i].Count;
            for (int i = 0; i < partitions4.Count; i++) partition4total += partitions4[i].Count;
            for (int i = 0; i < partitions5.Count; i++) partition5total += partitions5[i].Count;

            Console.WriteLine($"partition 1 total: {partition1total}");
            Console.WriteLine($"partition 2 total: {partition2total}");
            Console.WriteLine($"partition 3 total: {partition3total}");
            Console.WriteLine($"partition 4 total: {partition4total}");
            Console.WriteLine($"partition 5 total: {partition5total}");

            Assert.That(ex1.Count, Is.EqualTo(partition1total));
            Assert.That(ex2.Count, Is.EqualTo(partition2total));
            Assert.That(ex3.Count, Is.EqualTo(partition3total));
            Assert.That(ex4.Count, Is.EqualTo(partition4total));
            Assert.That(ex5.Count, Is.EqualTo(partition5total));
        }

        [Test]
        public async Task CompareDataLoadTest()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            List<DublicateModel> logs = await DublicatesController.GetLogsAsync();
            stopwatch.Stop();
            Console.WriteLine($"All data ({logs.Count}) loaded with {stopwatch.Elapsed.TotalSeconds} sec");

            stopwatch.Restart();
            var logsData = DublicatesController.GetLogsData();
            Console.WriteLine($"Url data ({logsData.Count}) loaded with {stopwatch.Elapsed.TotalSeconds} sec");
        }
    }
}