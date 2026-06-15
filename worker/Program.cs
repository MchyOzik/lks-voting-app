using System;
using System.Data.Common;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Newtonsoft.Json;
using Npgsql;
using StackExchange.Redis;

namespace Worker
{
    public class Program
    {
        public static int Main(string[] args)
        {
            try
            {
                Console.WriteLine("=== ENV DEBUG ===");
                Console.WriteLine($"POSTGRES_CONNECTION_STRING: {Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")}");
                Console.WriteLine($"REDIS_HOST: {Environment.GetEnvironmentVariable("REDIS_HOST")}");
                Console.WriteLine("=================");

                var pgConnStr = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING");

                if (string.IsNullOrWhiteSpace(pgConnStr))
                    throw new Exception("❌ POSTGRES_CONNECTION_STRING IS EMPTY!");

                var redisHost = Environment.GetEnvironmentVariable("REDIS_HOST");

                if (string.IsNullOrWhiteSpace(redisHost))
                    throw new Exception("❌ REDIS_HOST IS EMPTY!");

                var pgsql = OpenDbConnection(pgConnStr);
                var redisConn = OpenRedisConnection(redisHost);

                var redis = redisConn.GetDatabase();

                var keepAliveCommand = pgsql.CreateCommand();
                keepAliveCommand.CommandText = "SELECT 1";

                var definition = new { vote = "", voter_id = "" };

                while (true)
                {
                    Thread.Sleep(100);

                    if (redisConn == null || !redisConn.IsConnected)
                    {
                        Console.WriteLine("🔄 Reconnecting Redis...");
                        redisConn = OpenRedisConnection(redisHost);
                        redis = redisConn.GetDatabase();
                    }

                    string json = redis.ListLeftPopAsync("votes").Result;

                    if (json != null)
                    {
                        var vote = JsonConvert.DeserializeAnonymousType(json, definition);
                        Console.WriteLine($"🗳 Processing vote '{vote.vote}' by '{vote.voter_id}'");

                        if (pgsql.State != System.Data.ConnectionState.Open)
                        {
                            Console.WriteLine("🔄 Reconnecting DB...");
                            pgsql = OpenDbConnection(pgConnStr);
                        }
                        else
                        {
                            UpdateVote(pgsql, vote.voter_id, vote.vote);
                        }
                    }
                    else
                    {
                        keepAliveCommand.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("💥 FATAL ERROR");
                Console.Error.WriteLine(ex.ToString());
                return 1;
            }
        }

        private static NpgsqlConnection OpenDbConnection(string connectionString)
        {
            while (true)
            {
                try
                {
                    Console.WriteLine("🔌 Connecting to PostgreSQL...");

                    var connection = new NpgsqlConnection(connectionString);
                    connection.Open();

                    Console.WriteLine("✅ Connected to PostgreSQL");

                    var command = connection.CreateCommand();
                    command.CommandText = @"
                        CREATE TABLE IF NOT EXISTS votes (
                            id VARCHAR(255) NOT NULL UNIQUE,
                            vote VARCHAR(255) NOT NULL
                        )";
                    command.ExecuteNonQuery();

                    return connection;
                }
                catch (SocketException ex)
                {
                    Console.Error.WriteLine("❌ DB SOCKET ERROR");
                    Console.Error.WriteLine(ex.Message);
                    Thread.Sleep(2000);
                }
                catch (DbException ex)
                {
                    Console.Error.WriteLine("❌ DB ERROR");
                    Console.Error.WriteLine(ex.Message);
                    Thread.Sleep(2000);
                }
            }
        }

        private static ConnectionMultiplexer OpenRedisConnection(string hostname)
        {
            while (true)
            {
                try
                {
                    var ipAddress = GetIp(hostname);

                    Console.WriteLine($"🔌 Connecting to Redis: {ipAddress}");

                    var conn = ConnectionMultiplexer.Connect(ipAddress);

                    Console.WriteLine("✅ Connected to Redis");

                    return conn;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("❌ REDIS CONNECTION ERROR");
                    Console.Error.WriteLine(ex.Message);
                    Thread.Sleep(2000);
                }
            }
        }

        private static string GetIp(string hostname)
        {
            try
            {
                return Dns.GetHostEntry(hostname)
                    .AddressList
                    .First(a => a.AddressFamily == AddressFamily.InterNetwork)
                    .ToString();
            }
            catch (Exception ex)
            {
                throw new Exception($"❌ DNS RESOLVE FAILED for {hostname}: {ex.Message}");
            }
        }

        private static void UpdateVote(NpgsqlConnection connection, string voterId, string vote)
        {
            try
            {
                var command = connection.CreateCommand();
                command.CommandText = "INSERT INTO votes (id, vote) VALUES (@id, @vote)";
                command.Parameters.AddWithValue("@id", voterId);
                command.Parameters.AddWithValue("@vote", vote);
                command.ExecuteNonQuery();
            }
            catch (DbException)
            {
                var command = connection.CreateCommand();
                command.CommandText = "UPDATE votes SET vote = @vote WHERE id = @id";
                command.Parameters.AddWithValue("@id", voterId);
                command.Parameters.AddWithValue("@vote", vote);
                command.ExecuteNonQuery();
            }
        }
    }
}