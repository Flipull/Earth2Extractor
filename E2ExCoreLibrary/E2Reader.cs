using E2ExCoreLibrary.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;

namespace E2ExCoreLibrary
{
    static public class E2Reader
    {
        static string LB_URL = "https://app.earth2.io/leaderboards/player?";
        static string MP_URL = "https://app.earth2.io/marketplace?";
        static string QL_URL = "https://app.earth2.io/graphql?";
        static string USR_URL = "https://app.earth2.io/api/v2/user_info/";


        static public User ReadUser(string id)
        {
            string json = string.Empty;
            string query = USR_URL + id;

            HttpWebRequest request =
                (HttpWebRequest)WebRequest.Create(query); //Uri.EscapeUriString(query));
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            //request.Headers.Add("Content-Transfer-Encoding", "binary");

            bool success = false;
            while (!success)
            {
                try
                {
                    using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                    using (Stream stream = response.GetResponseStream())
                    using (StreamReader reader = new StreamReader(stream))
                        json = reader.ReadToEnd();
                }
                catch (System.Net.WebException e)
                {
                    System.Threading.Thread.Sleep(5000);
                }
                finally { success = true; }
            }

            return JsonConvert.DeserializeObject<User>(json);
        }
        static public LandField ReadLandField(string id)
        {
            string json = string.Empty;
            string query = QL_URL + "query={getLandfieldDetail(landfieldId:\"" + id +
                    "\"){tileClass,tileCount," +
                    "transactionSet{id,price,time,owner{id},previousOwner{id}}," +
                    "bidentrySet{buyer{id}}}}";

            HttpWebRequest request =
                (HttpWebRequest)WebRequest.Create(query); //Uri.EscapeUriString(query));
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            //request.Headers.Add("Content-Transfer-Encoding", "binary");

            bool success = false;
            while (!success)
            {
                try
                {
                    using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                    using (Stream stream = response.GetResponseStream())
                    using (StreamReader reader = new StreamReader(stream))
                        json = reader.ReadToEnd();
                }
                catch (System.Net.WebException e)
                {
                    System.Threading.Thread.Sleep(5000);
                }
                finally { success = true; }
            }

            JObject job = (JObject)JsonConvert.DeserializeObject(json);
            LandField result = job.First.First.First.First.ToObject<LandField>();
            result.id = id;
            //Console.WriteLine(result);

            return result;
        }
        static public List<User> ReadLeaderboardUsers(string country = "", string sort = "")
        {
            string json = string.Empty;
            HttpWebRequest request =
                (HttpWebRequest)WebRequest.Create(LB_URL + "country=" + country + "&sort=" + sort);
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream))
                json = reader.ReadToEnd();

            List<User> result = JsonConvert.DeserializeObject<List<User>>(json);
            //Console.WriteLine(json);

            return result;
        }


        static public List<LandField> ReadMarketPlace(string country, int page = 1, string sort = "-price")
        {//tileClass=1 ))
            string json = string.Empty;

            HttpWebRequest request =
                (HttpWebRequest)WebRequest.Create(MP_URL + "country=" + country + "&page=" + page + "&items=25&sorting=" + sort);
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

            bool success = false;
            while (!success)
            {
                try
                {
                    using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                    using (Stream stream = response.GetResponseStream())
                    using (StreamReader reader = new StreamReader(stream))
                        json = reader.ReadToEnd();
                }
                catch (System.Net.WebException e)
                {
                    Console.WriteLine(e.ToString());
                    System.Threading.Thread.Sleep(5000);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                    System.Threading.Thread.Sleep(30000);
                }
                finally { success = true; }
            }

            var job = (JObject)JsonConvert.DeserializeObject(json);
            List<LandField> result = job["landfields"].ToObject<List<LandField>>();
            //List<LandField> result = JsonConvert.DeserializeObject<List<LandField>>(json);
            return result;
        }

        static public Tuple<int,List<LandField>> ReadUserLandFieldsV(string id, int page = 1)
        {
            string json = string.Empty;
            string query = QL_URL + "query={getUserLandfields(userId:\"" + id + "\"" +
                    ",items:100,page:" + page +
                    "){count,landfields{id,tileClass,tileCount," +
                    "transactionSet{id,price,time,owner{id},previousOwner{id}}," +
                    "bidentrySet{buyer{id}}}}}";

            HttpWebRequest request =
                (HttpWebRequest)WebRequest.Create(query); //Uri.EscapeUriString(query));
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            //request.Headers.Add("Content-Transfer-Encoding", "binary");

            bool success = false;
            while (!success)
            {
                try
                {
                    using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                    using (Stream stream = response.GetResponseStream())
                    using (StreamReader reader = new StreamReader(stream))
                        json = reader.ReadToEnd();
                }
                catch (System.Net.WebException e)
                {
                    Console.WriteLine(e.ToString());
                    System.Threading.Thread.Sleep(10000);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                    System.Threading.Thread.Sleep(30000);
                }
                finally { success = true; }
            }

            JObject job = (JObject)JsonConvert.DeserializeObject(json);
            int count = job["data"]["getUserLandfields"]["count"].ToObject<int>();
            List<LandField> result = job["data"]["getUserLandfields"]["landfields"].ToObject<List<LandField>>();

            return new Tuple<int, List<LandField>>(count, result);
        }

        static public List<LandField> ReadUserLandFields(string id, int page=1)
        {
            string json = string.Empty;
            string query = QL_URL + "query={getUserLandfields(userId:\"" + id + "\"" +
                    ",items:100,page:" + page +
                    "){landfields{id,tileClass,tileCount," +
                    "transactionSet{id,price,time,owner{id},previousOwner{id}}," +
                    "bidentrySet{buyer{id}}}}}";
            
            HttpWebRequest request =
                (HttpWebRequest)WebRequest.Create(query); //Uri.EscapeUriString(query));
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            //request.Headers.Add("Content-Transfer-Encoding", "binary");
            
            bool success = false;
            while (!success)
            {
                try
                {
                    using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                    using (Stream stream = response.GetResponseStream())
                    using (StreamReader reader = new StreamReader(stream))
                        json = reader.ReadToEnd();
                }
                catch (System.Net.WebException e)
                {
                    Console.WriteLine(e.ToString());
                    System.Threading.Thread.Sleep(10000);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                    System.Threading.Thread.Sleep(30000);
                }
                finally { success = true; }
            }

            JObject job = (JObject)JsonConvert.DeserializeObject(json);
            List<LandField> result = job["data"]["getUserLandfields"]["landfields"].ToObject<List<LandField>>();

            return result;
        }

        static public int ReadUserLandFieldsCount(string id)
        {
            string json = string.Empty;
            string query = QL_URL + "query={getUserLandfields(userId:\"" + id + "\"" +
                    ",items:1,page:1){count}}";
            
            HttpWebRequest request =
                (HttpWebRequest)WebRequest.Create(query); //Uri.EscapeUriString(query));
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            //request.Headers.Add("Content-Transfer-Encoding", "binary");

            bool success = false;
            while (!success)
            {
                try
                {
                    using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                    using (Stream stream = response.GetResponseStream())
                    using (StreamReader reader = new StreamReader(stream))
                        json = reader.ReadToEnd();
                }
                catch (System.Net.WebException e)
                {
                    Console.WriteLine(e.ToString());
                    System.Threading.Thread.Sleep(10000);
                }
                catch(Exception e)
                {
                    Console.WriteLine(e.ToString());
                    System.Threading.Thread.Sleep(30000);
                }
                finally { success = true; }
            }

            JObject job = (JObject)JsonConvert.DeserializeObject(json);
            int count = job["data"]["getUserLandfields"]["count"].ToObject<int>();

            return count;
        }












    }
}
