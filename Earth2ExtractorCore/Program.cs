using E2ExCoreLibrary;
using E2ExCoreLibrary.Model;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Earth2ExtractorCore
{
    class Program
    {
        static string sanitizeName(string name)
        {
            return name.Replace(',', '.').Replace('"', '.').Replace('\'', '.').Replace('`', '.');
        }
        
        static void AddFieldToDB(LandField new_field)
        {
            var db = new E2DB();

            //if bidders not exists, save them
            foreach (var b in new_field.bidentrySet)
            {
                if (db.Users.Find(b.buyer.Id) == null)
                    db.Users.Add(b.buyer);
            }

            //if buyers/sellers not exists, save them
            foreach (var t in new_field.transactionSet)
            {
                if (db.Users.Find(t.owner.Id) == null)
                    db.Users.Add(t.owner);
                if (t.previousOwner != null && db.Users.Find(t.previousOwner.Id) == null)
                    db.Users.Add(t.previousOwner);
            }

            //if field not exist, save it
            var lf = db.LandFields.Include("transactionSet").Where(f => f.id == new_field.id).FirstOrDefault();
            if (lf == null)
            {
                db.LandFields.Add(new_field);
            }
            else
            {
                //update transactions
                foreach (var t in new_field.transactionSet)
                {
                    if (db.Transactions.Find(t.id) == null)
                    {
                        lf.transactionSet.Add(t);
                        db.Transactions.Add(t);
                    }
                }
                db.LandFields.Update(lf);
            }
            db.SaveChanges();
        }
        
        static void AddUsersToDB(List<User> users)
        {
            var db = new E2DB();

            foreach(User u in users)
            {
                User user = db.Find<User>(u.Id);
                if (user == null)
                {
                    db.Users.Add(u);
                } else
                    if (user.name == null)
                    {
                        user.name = u.name;
                        db.Users.Update(user);
                    }
            }

            db.SaveChanges();
        }
        static void ReadAllLeaderboardUsers() {
            Console.WriteLine("Leaderboards (World)");
            //worldwide
            AddUsersToDB(E2Reader.ReadLeaderboardUsers(string.Empty, "networth"));
            AddUsersToDB(E2Reader.ReadLeaderboardUsers(string.Empty, "tiles"));
            
            //per country
            foreach (string c in Constants.Countries)
            {
                Thread.Sleep(600);
                Console.WriteLine("Leaderboards ("+c+")");
                AddUsersToDB(E2Reader.ReadLeaderboardUsers(c, "networth"));
                AddUsersToDB(E2Reader.ReadLeaderboardUsers(c, "tiles"));
            }
        }
        static void UpdateAllUsers()//name, country
        {
            
        }
        static void ReadAllMarketPlaceOffers(int page_range=1)
        {
            if (page_range < 1) page_range = 1;
            
            var r = new Random();
            Constants.Countries.Shuffle();
            foreach(var c in Constants.Countries)
            {
                Console.WriteLine("MarketPlace (" + c + ")");
                int resultcount = 25;
                int page = r.Next(0, page_range-1);
                string sort = Constants.MPSorting[r.Next(0, 4)];
                var fields = E2Reader.ReadMarketPlace(c, page, sort);
                Thread.Sleep(600);
                var db = new E2DB();
                foreach (var field in fields)
                {
                    if (db.LandFields.Find(field.id) == null)
                    {
                        var new_field = E2Reader.ReadLandField(field.id);
                        AddFieldToDB(new_field);
                    }
                }

                page++;
                resultcount = fields.Count;
            }

        }
        
        static void ReadUsersLandfields(string id)
        {
            if (id == "039813cf-3691-4958-976a-e344bc4c35d2")
                return;

            var db = new E2DB();

            var fieldsdata = E2Reader.ReadUserLandFieldsV(id, 1);
            var count = fieldsdata.Item1;
            var fields = fieldsdata.Item2;

            Console.WriteLine(" UserFields (" + id + " has " + count + ")");

            foreach (var new_field in fields)
                AddFieldToDB(new_field);

            int max_page = count / 100 + 1;
            int page = 2;
            while (page <= max_page)
            {
                fields = E2Reader.ReadUserLandFields(id, page);
                foreach (var new_field in fields)
                    AddFieldToDB(new_field);
                page++;

            };
            db.SaveChanges();
        }


        static void ReadAllUsersLandfields()
        {
            var r = new Random();
            var db = new E2DB();
            
            while(true)
            {

                var userlist = db.Users.OrderBy(u => u.updated)
                                    //.ThenByDescending(u => u.name)
                                    .Where(u => !u.locked &&
                                                (!u.updated.HasValue || u.updated.Value <= DateTime.Now.AddHours(-24) ) )
                                    .Take(40).ToList();
                var user = userlist[r.Next(0, userlist.Count-1)];//crashes on empty list
                user.locked = true;
                db.SaveChanges();

                var fieldsdata = E2Reader.ReadUserLandFieldsV(user.Id, 1);
                Thread.Sleep(600);
                var count = fieldsdata.Item1;
                var fields = fieldsdata.Item2;

                Console.WriteLine(" UserFields (" + user.Id + " has " + count + ")");

                foreach (var new_field in fields)
                    AddFieldToDB(new_field);

                int max_page = count / 100 + 1;
                int page = 2;
                while (page <= max_page)
                {
                    fields = E2Reader.ReadUserLandFields(user.Id, page);
                    foreach (var new_field in fields)
                        AddFieldToDB(new_field);
                    page++;

                };
                user.updated = DateTime.Now;
                user.locked = false;
                db.SaveChanges();
            }
        }




        static DateTime FirstDayOfNextMonth(DateTime d)
        {
            return d.AddMonths(1).AddDays(-d.Day + 1);
        }
        static void MakeUserSimpleStatistics(User user, DateTime start, DateTime end)
        {
            var db = new E2DB();
            if (db.Simpletons.Where(s => s.userid == user.Id && s.momenta == end).FirstOrDefault() != null)
                return;

            var new_stats = new SimpleData();
            var tr = db.Transactions.Include("landField")
                                    .Where(t => t.ownerId == user.Id || t.previousOwnerId == user.Id)
                                    .AsEnumerable()
                                    .Where(t => t.moment >= start && t.moment < end)
                                    .OrderBy(t => t.moment)
                                    .ToList();

            var lf = new List<LandField>();
            tr.ForEach(t => {
                if (!lf.Contains(t.landField))
                    lf.Add(t.landField);
            });
            var filtered_lf = new List<LandField>();
            foreach (var l in lf)
            {
                bool valid = true;
                foreach(var t in l.transactionSet)
                    if (l.transactionSet.FindAll(t2 => t2.ownerId == t.ownerId).Count > 1)
                    {
                        valid = false;
                        break;
                    }
                if (valid)
                    filtered_lf.Add(l);
            }

            Console.WriteLine("User (" + user.Id + ", has " + lf.Count + " lfs)");

            new_stats.userid = user.Id;
            new_stats.momenta = end;//new DateTime(snapdate.Ticks);



            foreach (var l in lf)
            {
                var trans_set = l.transactionSet.OrderBy(t => t.moment).ToList();
                var last_tr = trans_set[trans_set.Count - 1];
                if (last_tr.ownerId == user.Id)
                {
                    new_stats.tilesBoughtAmount += l.tileCount;
                    new_stats.tilesCurrentlyOwned += l.tileCount;
                    new_stats.currentPropertiesOwned += 1;
                }
                else if (last_tr.previousOwnerId == user.Id)
                {
                    new_stats.tilesBoughtAmount += l.tileCount;
                    new_stats.tilesSoldAmount += l.tileCount;
                }

                new_stats.totalUniquePropertiesOwned += 1;

                for (int i = 0; i < l.transactionSet.Count; i++)
                {
                    var t = trans_set[i];

                    if (t.ownerId == user.Id)
                    {//was bought
                        new_stats.totalPropertiesOwned += 1;
                    }
                    else if (t.previousOwnerId == user.Id)
                    {//was sold
                        var bought_transaction =
                            db.Transactions.Where(t => t.landField == l && t.ownerId == user.Id)
                                        .AsEnumerable()
                                        .OrderByDescending(t => t.moment)
                                        .FirstOrDefault();
                        if (bought_transaction == null)
                            continue;

                        new_stats.totalPropertiesResold += 1;
                        new_stats.profitsOnSell += t.price - bought_transaction.price;
                        new_stats.returnsOnSell += (double)(t.price - bought_transaction.price) / (double)bought_transaction.price;
                        if (double.IsNaN(new_stats.returnsOnSell))
                            throw new Exception();
                    }
                }
            }
            db.Simpletons.Add(new_stats);
            db.SaveChanges();
        }

        static void MakeAllStatistics()
        {
            var db = new E2DB();

            //create stats for every month available

            //get range of data
            var oldest_updated_user = db.Users.OrderBy(u => u.updated).First();

            if (!oldest_updated_user.updated.HasValue)
                throw new Exception("No valid user-dates found");

            var newest_completedata_date = oldest_updated_user.updated.Value;

            var oldest_transaction =
                db.Transactions.FromSqlRaw<LandFieldTransactions>(
                    "select top(1) * from Transactions order by cast(time as datetime2)")
                .First();
            var oldest_data_date = oldest_transaction.moment;
            List<DateTime> momenta = new List<DateTime>();
            var list_loop_date = FirstDayOfNextMonth(oldest_data_date.Date);
            while (list_loop_date < newest_completedata_date.Date)
            {
                MakeStatistics(list_loop_date.AddMonths(-1), list_loop_date);
                list_loop_date = FirstDayOfNextMonth(list_loop_date);
            }
        }


 /*
        static void MakeNewestStatistics()
        {
            var db = new E2DB();
            var oldest_data = db.Users.OrderBy(u => u.updated).First();
            if (oldest_data == null || !oldest_data.updated.HasValue)
                return;
            MakeStatistics(oldest_data.updated.Value);
        }
        */
        static void MakeStatistics(DateTime start, DateTime end)
        {
            var db = new E2DB();

            //SimpleData
            var oldest_data = db.Users.OrderBy(u => u.updated).First();
            if (oldest_data == null || !oldest_data.updated.HasValue
                || oldest_data.updated.Value < end)
                return;

            
            foreach (var user in db.Users.AsEnumerable() )
            {
                while (ThreadPool.PendingWorkItemCount > 100)
                    Thread.Sleep(750);
                ThreadPool.QueueUserWorkItem(s => MakeUserSimpleStatistics(user, start, end));
            }
            while (ThreadPool.PendingWorkItemCount > 0)
            {
                Console.WriteLine(ThreadPool.PendingWorkItemCount);
                Thread.Sleep(750);
            }
            
        }

        
        static void ReadUserDetails(string userid, bool skip = true)
        {
            var db = new E2DB();
            var user = db.Users.Find(userid);
            if (skip && user.customPhoto != null)
                return;
            Console.WriteLine("ReadUser (" + userid + ")");
            var userdata = E2Reader.ReadUser(userid);
            Thread.Sleep(600);
            if (userdata.customPhoto == null || userdata.customPhoto == string.Empty)
            {
                user.customPhoto = userdata.picture;
            }
            else
            {
                user.customPhoto = userdata.customPhoto;
            }
            user.name = userdata.username;
            user.countryCode = userdata.countryCode.ToUpperInvariant();
            db.SaveChanges();
        }
        static void ReadAllUserDetails()
        {
            var db = new E2DB();
            
            foreach (var user in db.Users.ToList())
            {
                if (user.customPhoto != null)
                    continue;
                Thread.Sleep(600);
                Console.WriteLine("ReadUser (" + user.Id + ")");
                var userdata = E2Reader.ReadUser(user.Id);
                if (userdata.customPhoto == null || userdata.customPhoto == string.Empty)
                {
                    user.customPhoto = userdata.picture;
                } else
                {
                    user.customPhoto = userdata.customPhoto;
                }
                user.name = sanitizeName(userdata.username);
                user.countryCode = userdata.countryCode.ToUpperInvariant();
                db.SaveChanges();
            }
        }

        ////////////////////////////////////////////
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            Console.ReadLine();

            //ReadAllLeaderboardUsers();

            //Constants.Countries.Clear();
            //Constants.Countries.AddRange(Constants.RichCountries);
            //while (true)
            //    ReadAllMarketPlaceOffers(page_range:10);
            
            //foreach (var v in new List<string> { "5c9b40bb-bce9-40bc-a69d-fbdcd6cbcda7", "34a32f88-23ad-429c-af25-733872562967", "b8faec8c-7cd5-48dc-846d-0f2af3ba0249", "eca72dd0-6aca-4fd3-8b46-9464659c8fb6", "23c0d4f7-0d5a-4f0a-bda4-8bbb0c9bfbac", "55e9e78c-e0a6-45a2-9520-edd11c576477", "39375f76-e8fd-41d7-9c24-cbbbba2d9121", "e6f4fa18-9d18-410b-9823-713bc1bf7b2c", "2c23f27a-24fc-4d85-9885-69f44f3213e7", "e9436e82-4c20-491e-a8dc-d45e2416e90e", "e0816e2d-8ece-46d6-b91b-032ce05a19b8", "9d2154ad-59b4-48da-98e0-90f4165ffb36", "fcc186ce-1201-4b49-a59b-a5bc2d45e0e0", "635bb76c-b22f-42a9-aa4a-64c366696e9e", "f0d71769-0212-46a8-95bf-c242e9cfe965", "a696c222-4f41-4ab2-8831-72972e493804", "0e5b4e2a-6093-49cc-a16a-c4fb92b09728", "34893f98-5be2-4d9c-ab78-6a81fdaad352", "47497593-2e17-4529-90bf-a513d01b7da5", "f682a917-a604-468c-ad9f-fa2e12b80bde", "dbb576d1-825f-43e6-9ab3-bf1598e647db", "a6dd8ffe-320c-4bac-9006-aa5fdaaa3e2f", "bcafefe3-8478-494c-bec5-ddef86440689", "f418e884-72ca-4d6d-970e-0c2dde3134db", "dbf37f52-a0c7-4b43-ae52-7327dda597e9", "83c75501-8237-470f-873f-63dfd8bb274e", "02c3d02d-1259-4fa4-94fe-febde704679a", "c5850beb-6e3d-4670-9685-7aefc0c55ebd", "ab5aa398-b42b-47a3-8a09-f9709db6b368", "f1895b13-beff-4929-9607-0db7fdd4f3ee", "aeca1901-0d96-4476-b29b-ba33d0c9d739", "df51fd18-5efb-48ee-ae06-9db4929db598", "cea8ea76-c6ba-478b-8a47-602ac6f338be", "7cc7ae6a-3540-49e9-a069-e0455d161439", "bc5a192c-ffed-4725-9a71-63b787f612ee", "53398049-9e3b-4d0f-b5ff-dfd0e4f903c1", "6912fd60-2b85-4612-bba6-d092cb2aa3b0", "c5d5c0e2-86f6-46af-8143-9cf18f57975c", "e55f9df8-a1ad-492c-a79f-c1826175bc5f", "532cbba5-39b6-445f-bed4-f3eaf66865da", "9b7dcc2c-d42a-4794-b0e8-3b370ea71d75", "0fa9a508-78b3-424a-9777-1d6147c8067c", "9bac4008-a3f2-4674-a93e-c177b329ff57", "2a36a36d-5119-42b5-88e7-e24cfed878fd", "a61aee1b-ef3b-406b-8210-f9a9a0b56f16", "684e1459-0bec-4b22-b490-eafc707760c2", "2af56ff5-2c67-485b-b998-0801b61f13f9", "90947307-bd58-401c-8dd6-cb51c8d09277", "54c88945-9587-4e84-bc2f-fe111a1e6f58", "5d724f73-5e50-4d3c-9152-0062aa386501", "2434219d-9771-4190-8704-6c0675f57ce7", "fa544c10-708e-406a-a5d7-fa8bba98dbac", "4019c86c-5ce8-4e00-838b-1429b84b3190", "94e2190b-c1b8-43f7-8a7b-ca87f4bdf0d7", "56bf3532-dbaa-4d84-b265-0d4094a792a7", "8964fff8-1d74-4411-983d-51375d4778a0", "991e394c-ad53-4358-b503-1ffb6c591035", "51cedf59-55a4-4538-b391-bef94da523ad", "d5b11d8e-406a-40dc-9468-f95e8c5b24ed", "66c758d6-81de-4a95-b6c5-ca75fad447d8", "044051af-568d-41d1-bded-6300c142fda5", "7c4d4a97-d6fd-41d7-bfed-17d3ead0eb6a", "ffe27984-5899-4448-b793-f82cde7891c9", "55690042-d407-4594-ac1f-fa912b860ed8", "2c11f6f2-59dc-498f-a82f-d47d9e24f31a", "b6a22aee-e349-4df3-814f-cefd42fbf7cf", "1d1c171e-1969-47c4-8881-b76ff617e484", "1342bae8-63aa-46d1-9275-92d4eae38adb", "0a20cd5a-f7ef-465d-8f1a-504a0fe787dc", "b73eece2-94a1-4d4b-a893-b6aee9cec6b3", "a7db368d-5bc8-4c5e-adeb-c68984836195", "84c11b0c-f6ad-4664-ab11-913c5bdbdc4a", "b6f22d8c-67e3-45e8-90aa-bc5501c791a9", "f644a2fd-1b60-4398-a449-cca93b5c6741", "417afd4c-4ef4-46be-b54d-8e9167e40863", "a9105c38-7f58-43f7-aaa4-d063c2429197", "a64f4a23-4a02-488a-9e7f-aa8aafe27cd2", "776dd401-f92d-47c3-8a77-cb9c5bcc32cd", "986ec690-5e4a-4b0b-b9a3-b2fca627bfb4", "34688500-1b20-4d86-8941-ae5efce27b2c", "2713a679-a8b2-4246-b216-bc98442d7ad8", "76196547-d771-4c21-ae85-a97c1482758e", "b31bb45d-c9b9-4f7c-b96c-d276f2672b56", "51c3d94d-de2a-4e62-a98a-453ab916c799", "2757b43c-dc5d-4dd5-962e-7efab9dc9cc9", "9e9add6a-df4f-4be6-88d9-ab602749a817", "e94eb09d-2b5b-442a-9c11-854a2e6b9c06", "21caf6e5-79c6-494e-bd6c-a44f4533eaad", "db1157b7-26a9-4e94-b841-e8c0ef9a298f", "59cc59a5-3369-4857-ae78-312f4d23079a", "a7470b86-42e0-44dd-b946-d10f3e68309a", "d8b19b13-c641-4ae1-b299-28dbcee2034e", "3be73e69-327d-490c-9c1f-cd0c0259eed4", "3132f65b-5306-4cc3-a0be-981a66730ed4", "d5a50d28-a854-4d20-9e40-762cf40133d4", "b2b7dbe0-78be-4db7-95f5-ff07caf5ea7a", "5c9b6c9f-7a19-49e5-bc00-67f15f036ddb", "34148ddf-f287-4466-8757-53a116b68ab0", "68e38fc1-f7bd-469e-a8d8-9ca17cb365ef", "1e2e7493-334e-4c65-89b5-e482c7df4b67", "21ed0048-327e-41d4-ad1b-637361c2edf8", "30900c28-aedc-404b-a2c9-8cd9dd2a3bc6", "e3ada3d5-ca63-4357-b886-4abc9fc19f46", "a229893a-7b15-4b1b-ad84-ed734296036b", "71847c37-69f2-4f01-9b8d-1895d4699b98", "0f857535-b689-4abf-a6de-b91a227baa7e", "015ca78f-21a0-4806-830b-d0f8b7dc1097", "c70a72bc-187e-41d5-93a7-888420d3ade5", "72222c62-f056-43e5-8a46-6c5618ac1a19", "4863999e-bff5-460d-9f2d-96b4f84c58dc", "623f0570-57bc-4299-af17-0056e4eec996", "38e55a7d-6f97-4660-8582-fc2ede714006", "ff8c1f8c-89e0-4ff8-8acc-dfce6209a497", "5f8ca598-52e4-4020-8e6d-8a9d2e6f6532", "5754c755-4abf-47a5-88be-64b31eb9ea09", "a29994ba-11c5-4b23-be52-3a2724902028", "8bac0a5a-2ff1-46ab-b4f4-cd259b83bee1", "961429b1-2c3c-4d61-8a29-1d7c53fa94d8", "dd7f482e-e180-4846-9570-0f54fd25ffbd", "95b38c3e-6e01-48a8-a889-9889a87d511b", "89a993a1-e34a-40bf-8e0a-0159826bd15b", "ceecf0df-cee9-435e-90da-9008ee9504ed", "cdcb11bb-0e3d-45f5-9860-d43c2cd8cf8a", "ba0c5e87-1ddc-41d6-bdca-cc620a691aac", "fd6f90a1-700e-4c34-9618-6743664b3d0c", "f8810d3a-afd8-4897-a5d5-4f526ebcd639", "496f3f66-adef-4bae-8455-61ac6d34e588", "f046828b-fa3a-46a9-b19f-59078bdc2325", "f3462b31-3b80-41da-94ca-80d18c4a4a3d", "e9cf11d7-9090-41f9-b8a6-a47d939bdb6a", "3d723e48-6e60-4d39-9dd5-77c86b2d8f43", "6f7be817-8201-45aa-ad9f-da98c467a17c", "890b16dd-0cd4-4277-9fa3-e21e58b90fab", "dfe5c4ab-9e61-4784-9e8e-966f12a5b6ef", "b3587e9d-0f08-42e5-8d65-92b29c6781cf", "4226329f-9fa7-4597-844b-a7f01e5bf66e", "a34b945d-98d8-42bd-b161-0823f2955dc5", "f7aded5a-05a7-491d-94a6-1756d454d3ba", "2b726471-9abf-46e0-b6da-a50735833c65", "5c7a8986-f821-4763-87b2-a114e1235a52", "075f75ea-1e45-4276-9404-f6bc69bf145b", "9931b301-7a6a-445e-80dd-ef864dc58af3", "316a8bc6-027c-40a4-b25b-ebfcb985abe8", "c1052acf-c8c8-4d7b-bbf5-cd7131c9cbcb", "14ad13c1-6415-4067-a8da-4467ae34b49a", "f51efd42-ce1c-4a3b-9568-4d0e740fd3ad", "4064fc40-7f0f-4276-9b82-667d90a16e14", "c5105ae7-eb08-4a09-af6c-341d840fb8df", "b34e3f33-7593-4f57-b9f9-c337af53196c", "8cab01ec-5f2b-42af-980b-6ff8dd1afb2b", "c468981f-6866-4795-ab28-45b453791523", "828b282e-1284-48ba-8fe1-2ee0189b4a58", "07595f2a-9892-4f5f-ba06-c1b3c44d2075", "58f05f20-44d1-4f7b-9cc6-491464b8b12c", "dcdb1c84-0eff-4369-8f94-d7278045ba5d", "ca3d1658-c83f-481f-8bff-9846ddbcec1d", "31e3f59e-0607-4a91-ad54-6e0e609ce89a", "99a92b9f-326c-4164-a2ee-7b5d0ca7d6f8", "47288980-5b76-4a0a-915d-f787635b1561", "25df8d32-c3f8-4e26-af05-32a0cda01ebc", "e09ee053-3379-4e9d-aaab-709ef2f615c2", "e577e819-23e6-44d1-9fdc-d53f2d2553cd", "887056c8-a0ee-4386-bfc8-a3a0bf00dd5d", "cd80b1b3-e17c-4f0a-a123-19c1267bed9e", "0a273a61-7f71-4877-bbc4-5de6b2cfc0d7", "32e24d69-fc03-4cca-9fd1-09427b73b257", "d3363d7c-087c-4a4b-8fff-95c779b0815f", "7b01ea86-229a-457d-8af7-44459b9b170b", "368db349-674c-4df7-8f14-e334a5db30ac", "5b3a2fae-d594-4ef9-bc20-89d1e4b90ac0", "b02ec1cc-7aad-47ac-a67a-3dac90ff0eb4", "0b4a116d-62d0-45ec-afbc-a62157070959", "0771a937-9711-4212-9111-845349c68357", "a35cf97a-4044-435c-b04d-6923929137c0", "edec97a5-1c27-4d30-a64b-7ccc0d45cb0a", "cb64fe85-d0ae-4372-8aaf-7a5f80ad448a", "6aaae6c2-c724-4478-ab64-8374464fdfe3", "668de99c-8cde-4886-9c8a-4f9c2b9d4de9", "ce96bfcf-a338-408e-8a10-6c6f6afb01d3", "4d9ebd84-17f6-45f9-a456-bd5f53793c5a", "6889d72b-0c00-4437-9594-8c74676320d1", "7c4458db-b662-49e5-9816-64eeb9a69063", "96c00391-ab64-4035-bd23-6943eacd6a9b", "06a33dc3-0f28-4a5a-a1a4-0db050153a60", "f944124d-f30c-43b3-ac49-f8413f965b8a", "33e92193-878e-4fbb-852c-a4a797b05ce4", "6fc45b8d-705c-43a2-bae1-2b5af2d6941a", "bd3fad30-46e0-4abc-9943-9d04a1b7dadc", "64594aee-2a89-4cc4-966a-b21682bc0a75", "596070f7-7242-4318-8fa5-d460df527813", "779ae33b-06ec-40c5-8837-6b0d62d3ed93", "8d8c296e-1353-4b3f-ac42-d3af7af32fd6", "8ee53f18-2008-4603-8d3a-89186d5255fe", "5dbb6d36-6512-4343-ba75-81b4fe16c740", "560b48db-ce97-48ff-a450-2e442a77071c", "f3e47b33-834e-4f2e-b77c-cb7261a28daa", "ef7038e8-8d4a-4540-8a78-afc6b53cb81e", "e345f181-9ee7-4695-98dd-789c0a830b18", "85b7de0b-455c-42bb-88e9-5d4ccb35bc2d", "34bc4303-c158-4424-83e1-0b334e472baa", "c42f8b3c-4706-4f0d-8164-5e786f08da90", "36f0e049-cc1f-4410-ad10-5ec2573c5b3c", "de2796f6-347b-4264-831e-a04fd8f9254f", "38da5069-4582-45bb-8012-551a04316b70", "4db69edf-d0c8-4a8e-ac79-32517a802c9a", "8f20456f-9f76-40dd-9847-f5875806c1ed", "a4798d11-2491-4e86-b582-f8154c777b36", "d55ac36a-d624-4551-a4b6-c245a53855f3", "7f2b6278-af72-48bd-a7b0-7b11e0af2e17", "f751e73a-197a-4c0f-9bbd-959b8fe2c542", "a5c63761-e5c3-4482-8909-0ba1a33eddc3", "64ec7073-eec9-411f-b7bd-cd269c8ed5a2", "b800091e-8c41-435b-a141-d15b34b1040f", "02626011-cae4-40ba-9291-31add8037ee9", "6b89cde8-e70c-4517-b25c-b282669c10e6", "c889a42f-c57f-4114-8b81-9ca94930fe42", "7bfcc4db-9ce1-4aec-8c3b-b3bf3ac00be4", "3e32780d-8425-4e0f-b1d8-5077872a59e8", "fc4addcd-d836-4330-8ecf-2e09f0a85cb9", "61832145-1b60-409a-bef6-6dab049f586a", "10e0e29d-7d2f-4516-8d51-581ad9ccedb5", "6f7d350d-c2e8-4014-a1ec-10ed2e66f69c", "cc997554-0645-403c-8109-031d75600063", "4f703e0f-f940-405c-bb58-93cd589d8e7d", "0d6461f4-a9a1-4e49-a90b-d11f7ff80036", "20120472-f418-464c-886c-0f0686b4aadc", "a38ce0d1-3a21-4d43-9499-d17c14da668a", "216f7b22-9717-4782-b03d-5697d8eebec8", "79093172-d02c-4646-9c34-81d065ef5366", "566e2024-b960-4ebf-a941-d82d007cd2e1", "7c7b8a53-4cff-46ce-be5a-210f203671e1", "47dda1ee-d834-4b59-b800-7e52fd7582c3", "bb99d95c-ed7e-4808-aaa9-56a027bcc8ad", "746562f7-e20c-4e31-8c05-a8fcbcb35c96", "679e0f47-c13e-4c46-b4b0-46c0c0b3ec29", "aec612cd-09da-44b7-9d24-89e624ae1da3", "801e315b-79ec-480d-9816-52b6e2cad4c7", "735d7623-bf19-44e2-9d7f-d777efe8ac9b", "494b54d2-3251-4d9c-b195-4ea7904bb58f", "cfb5401d-c0f3-46b9-9ae9-7f593ef29d79", "6e8fb6ad-7f64-42ba-93c2-5e70b1e28d28", "4f0fc0a3-07a1-4457-867d-3b19c1eacd56", "a3d929a1-6de1-4f5a-a46a-ca9ebc18b0df", "9d3bf422-d655-41c1-8f5e-e8deee3d9644", "2da3c714-1337-4d92-8f44-73408be0549e", "427cc3da-ce7c-4d81-a377-14d8604b01c7", "e92147ad-65fb-4400-a509-426848982f2e", "2a533592-6fb3-49ae-953c-db06f0be8b76", "b0c83fdf-fe0c-4a13-8d7a-2b243963e4e2", "312c6f72-068c-4049-89a6-5922721a7d30", "cae59d5b-e2f4-4309-baf7-9b6523418766", "2bbcc8f5-ee24-4c2a-8446-c594d0f44aad", "58becd91-de91-4443-8500-97bc40cb1abb", "bfeae20f-f389-4c54-99c5-07feb7b83220", "72e32bb8-507c-425d-bda6-9593868b5ae6", "39dea3ee-089f-4787-a27a-bbec3534a9a2", "554f7783-a486-4638-9a47-24cf252e7fe2", "c6bf3f71-ce41-408e-b1a1-d86625e74774", "7dde6604-5252-4137-9054-bc82a433fb08", "58bd7f97-bfd9-4be2-8ca7-a95f7e3db52c", "3da23cd0-bb30-468f-b596-02f9b02266e4", "47fd89d8-4ee1-40e2-a92c-65434e51617d", "63c3b8f2-e6ff-4c3e-8e73-f528510f3bb1", "4bf832d8-3784-43ff-8320-1da52888125a", "3a2c7f7d-7dcc-476a-a053-2355827045f7", "74762a3c-8d0b-43f8-b350-11a74daf63d6", "a5e36160-994b-4b68-b926-9f2538b6506f", "11590b51-6256-442a-88bb-77b539a42b86", "32007b0c-0fb0-4c9f-b857-6dff94c43c49", "668b7895-56bc-412f-9a36-c7bdcab48887", "d5259c20-560d-46cb-8d68-b6eaf495eda8", "6e20c3d2-4f7d-4bbe-a26d-1ebb356088e5", "8e0ada8c-0776-47e5-a27c-6d905e3fedb2", "a98d7cac-5b37-4a8a-92b9-f375d44720b2", "6b77509a-5009-4e12-aff4-88132339afcf", "6cc00a6a-9228-4ca9-b99b-c105f7219d0f", "7203449a-a568-4825-ba62-7dac04753f25", "efc81b73-142c-4620-b726-c64ea29e70e3", "eeb1dedc-603a-41cf-8cb5-f4707d08840c", "14ba8929-a43d-4525-9ad1-62a14a349bda", "6882c706-830f-4378-9ece-43fb58f22eed", "54bb983f-9853-4e7c-97be-6f920cc7487a", "50439b6f-eabe-476d-bd76-415dedc6e389", "670f9346-91d0-4fee-bacd-5815d6062874", "f31d131e-e914-447f-844a-9b33f73a66e9", "35f7e8a6-ccdc-4fb1-9ddb-b7fc716e8f93", "0aaef922-2e0d-4a22-99fc-4559ec557675", "7346949a-f6be-4dcd-b378-58bff5848acf", "4e7cd4fd-b89b-4163-b8d7-6bf51e9cd390", "794dfb2b-85bd-471e-82e3-0d3c54c25959", "fbd140d2-c325-464a-9c2b-408a3691b871", "f3091d3e-f4c6-405c-a8a5-ccfb07a2e8c8", "ecb4a8f4-2744-4598-926e-7007a7da0657", "574d8cfa-ef00-44f0-9a21-cff704bfeb1a", "9f05ede7-da78-4737-b637-00662c72cca9", "d2bd0a78-fa07-4fdb-a70f-efdcc5323805", "a85cbc44-9ad1-4b14-9717-79283bbae427", "7cdb9739-831e-4161-bc6b-aafa740e190d", "6c943ef9-50a3-4632-aa84-5c16cb5dd986", "6101e9e4-f2be-4e9d-b3e5-67b847faccd5", "40abc8c2-f9f8-4510-a621-55b38a9c1a25", "acc02f36-33bb-4ec4-a6da-6b9754664b3c", "7b872a34-1830-4f71-a745-80f4b59a6cf1", "e5254f6d-456d-46a3-a06a-4ef2c72bf944", "b85dec4a-3ad2-488f-a650-269bfa52d10c", "6c08c96d-f5a2-452f-ad2c-f46a6fd01581", "7adb7fce-c449-440d-afaa-757307df1a51", "6342d7fb-6dbb-4651-9b1f-5e81eabdf28e", "ae348c24-bc10-4cbd-a591-3be29cb44e35", "0dd1f9d2-abc3-4ca6-8015-8aec53c46046", "c4b99dd9-0381-451e-b5a0-a65c930d4c10", "22e19a25-7e5a-42ba-8e3e-ff8195b91984", "da351dd0-e5f3-42d7-8a69-d2a8d1cb222c", "7ab2dde5-4645-4974-9cbd-a0a275c6997c", "eb19a7ef-917e-4d38-97d3-634d3160d229", "9902d5a9-b051-4adc-ad41-f92467e50c8f", "6a8aac9c-8044-4030-a985-b3d3b6c5e817", "dcf9ae0b-f84c-4294-8068-9eea7d171f96", "0a64e74e-1ce9-4028-bad1-f3064eb729cc", "3fce02c2-9e12-4ed0-b6db-76ec49aa1a0f", "47430c5d-0121-4513-b275-7e2f80fc6c8c", "5e791413-a51d-402e-8a1b-71147b1a603a", "db6ad2d3-072e-4c50-8e29-14d636389fab", "65386b7b-a517-475d-9796-9453c01d4e8a", "3ed8a0d1-0439-403c-9225-21767d1f874e", "bcd73701-3ca5-4913-a768-cf7d8f06ea5a", "c8fe0fa1-6cda-4745-b8d7-bbcb35e13bbe", "1e55d37a-1b0e-4490-9e0d-8cd676afd320", "fb88683c-349c-45fc-973a-d10af444d938", "fd573de0-b606-4ace-b698-f52ef0bd2658", "3351d263-b191-49a6-8e71-56492b0b91ba", "ef8acbd0-1614-43ae-bf43-751c1de69463", "525a6c6b-0986-420d-b7a4-b1786959a04e", "de254a03-da27-456d-be4e-948653b748b7", "d003bda3-ee39-41ee-87ee-224d6abcc185", "a31c5569-d233-416a-8be9-6aa0830cb7dd", "a2324945-27a6-4c0e-9d16-afbb7c4f834b", "518d87e7-15ca-4c50-9176-26b39bdbc7c6", "4aa5f869-4354-4aac-a8b5-23af411c65f8", "e0275ab1-98d3-45dd-8e3c-7b2b55ff777a", "aec65316-7a9c-4e7c-af60-a44b37ef3eb8", "b90b82dd-b64c-4608-bd25-32583c878b4b", "e38865ea-e037-42ae-bc40-70f775dba415", "cda56903-7858-4ea7-ae13-9fa6afdee8a2", "ec936a3c-6982-4078-8c46-55a058562fa6", "3d57be3c-5135-401b-bc36-57f899ab7d6d", "aee36126-d84d-4e51-803a-9cfaca719565", "efadff5c-7414-43d7-b7d3-c085951e2a15", "8bf9f437-3206-43ed-aaf8-2ab587b6d7fd", "873c62e3-5d72-4580-bffc-9bc7e9a6e644", "9cf6c62c-dc04-4c64-92cd-ad2c5d504fb1", "6983e945-a316-4c02-8b69-a13eb1e7b83d", "919e5884-a8d1-4ffb-9879-c0fa900c0b4b", "a464f45f-59da-4105-9cca-b9c07ac1d487", "9a084060-cf64-410e-972d-5d7c2a01f848", "589786c4-fb3b-4734-8f02-db58818bfef1", "5b361eac-18ca-4427-938b-4d5c9a6c7ce6", "b9436477-8ecd-45c3-a4a0-83baf13e9d4b", "3ce9997c-42ab-4d7b-bd33-e9851ca9b9d1", "51f632e1-d19a-4974-a9cc-d57c21c10300", "b9d86621-4d2e-4294-82c1-ae6a31c98e76", "5336ef2e-22c1-48f2-87fe-8ec76e0c6037", "b136cd7b-7b68-4fb3-979a-e7d91d9b1f78", "85b5ca86-7a3c-4c83-8c10-0934127db9fe", "eb9a876c-df4c-4880-8db5-9edd4735eac3", "474c3e15-561a-4bbb-b755-498ecde42998", "8104f311-0d07-4066-a57e-34c2ba909373", "043b660d-e026-4580-bb05-bad60f4a2481", "8fe33039-dfab-45d8-8c57-67fd8cdf6df0", "75c0619a-c9ae-4dfc-8a25-ec228204f268", "43541c3f-a19b-4b80-93b5-8b85ffb8d629", "d1761ba8-92ab-48ac-a60d-6f1ea5e2e9e7", "297b527b-f895-4a3e-9d75-565fd681b5af", "b431de8f-5bbd-4e87-979d-4bee5b5a6e89", "28de5536-5059-43b4-88f4-a4a81efcdc26", "73f65b3b-eca6-4b12-81fb-69983dae3279", "15984324-a6cd-419e-8eaa-26277fa9184a", "9505f432-faad-43db-9457-11d5a611b759", "8e00245f-1a35-4d88-b1b9-c3d822ddd4cc", "388ea970-46be-4fb6-a711-775976682f83", "b08e995e-a2bf-452c-be88-57891263d6ce", "d89e9b22-e74b-4a3b-8dd7-5aebec4e7702", "67e39dc8-2022-45be-9b32-5e695a3981af", "7c7c0031-6b47-4d53-8074-6c028e7e4f3c", "70f0691e-fff8-48c2-a1e4-26a5983ff098", "4468d011-9757-4dbe-b01e-b0fa82143076", "4e86fa4a-b692-4d66-bab6-7a3a0738618e", "64433c04-98d1-4fdd-926c-c1219a86f048", "edafb0cd-dd82-4c9d-ae61-e5e76153a660", "cf2ea099-5cd4-4de7-8752-6d088f110caa", "dcf5292c-c709-4822-bba4-94257687bb8a", "c5d6933c-aae7-4ff3-a4b8-7a6fa1441f87", "33bebd13-58fa-49fe-a3b6-a0247d0654ae", "8bdfa0c1-ba91-4272-a406-07b4789f3ad9", "c0cfb10f-cdf5-4ac7-bed1-9f9d6d698119", "cbb2505a-54de-4438-8491-887801c8c0a8", "9b413338-3e90-42db-9df7-8ad85c07002a", "da74ddbf-a830-434a-ab02-cd1398f371ce", "72eba24d-2b45-4c17-89e0-ed6c89dbed0d", "c1ee9ba4-cc8d-4c71-a4eb-a4232b35ce54", "75bbc43e-a2ca-41e0-a615-3e15e75502bf", "d72b07d5-1161-458c-96c9-be55d1325817", "d3420ba3-91d7-457d-8c64-ce2e2c102f1d", "eded9965-cbbf-43e2-8cbd-dcf733959fbf", "ee3b2978-450b-4e12-ae45-592e5baa85de", "50671b2d-f0d1-4d65-abc8-a8fe4431419e", "8421670a-b24b-4f5e-a058-962bf652e2ce", "c1021cd1-de73-4c61-923d-4733ba7233cd", "be7de409-15f7-47fa-bd84-0c176fae0354", "9860523a-adea-420b-b6f6-109ff1fcbb77", "ec2d9a52-8c81-435f-87bc-e95893bf99ef", "fdd96f11-7f9f-4e98-a0ec-77c91fc84bfb", "35bcdaa4-2951-4f4a-b5c4-67041fd66c35", "66173212-87f0-43c6-a81a-5ced133dc73c", "619c9dfb-b41b-4dee-b5eb-f38893ec521d", "bae1c5e0-127e-4638-bfe8-2174d16de2f7", "04a6e9d4-51cd-4115-a437-7beec01a74fe", "af4e91a7-78e7-4cdb-97f2-1136afa33dd7", "62583763-2ab3-43ce-aa5b-897dbe428cd5", "3c3b3f9c-b006-497b-b1f2-002c46f067f5", "085c5ed3-7b8f-4711-82ee-c6cabe371f99", "f22a77b3-2be7-48b0-abf4-5d3aa93f2bd1", "f1e9e879-6320-4088-bfd5-30eea9fbf5c9", "7ab56ac6-a365-4970-bb81-6615225ffd5c", "3707f59b-7452-43ae-85f2-625e62028684", "5e8fa458-5248-4f3e-b7ed-806d1a620d0a", "fce41933-9722-442b-9fc6-d49046a0c88f", "822a3e1a-f95a-440a-80fa-034d05ef5973", "d84d47cc-0439-428b-ba8e-85bd5fa7f919", "76f37b64-6be8-4b4f-b7aa-b30a7e3e1f45", "63da7424-7dbd-4a07-a1ff-7fe0121f88e8", "110de81c-40d7-4ea8-ae9d-a9307d56724b", "0d5ac233-306c-48eb-abef-d158182310fe", "b8c8f9f5-1c62-4074-9846-a90161e3acb5", "3fd5e0f1-ea9d-4600-a79e-f308b3ac364d", "b8ff3633-70b5-420f-9cd7-b9b3cd890a5c", "f3579877-2292-4310-8ddf-a7f02b8d330a", "878d6627-9f37-4284-b09f-22efd35aaf0e", "eb7b820a-5b4c-4428-8271-326276956a81", "3cd7edd3-3565-429a-9121-fa1d33812572", "631c0eb8-d411-4d3a-a3e9-0caa61216d86", "6841a6a6-ec9f-4a03-a840-b3d3173db956", "fff3b501-5b74-4ff8-9086-e90cfbc8d7d6", "7b167474-45ec-4d4c-9a4e-8b168e6efd76", "82cdf195-9bd2-4a19-979e-f9e4dc0e4e34", "dc2a29b8-e579-4d53-906c-eec98b25d69a", "a59186a4-bfca-476b-82a6-404d23a12277", "3548e86a-93d5-4132-a898-9b82816b20a1", "52b26c97-83f1-4898-bbfc-dc8f2f62bcfd", "9edea177-7e11-4db8-a757-fa6e990e6c22", "6ce20e9a-26b0-4243-bd89-86c44a69c451", "8629d463-cf7c-48bd-bcd9-17c2546581fd", "7dc8e9ab-bf9d-41e1-b1c6-091c62f47c1b", "7a2c09a2-3e15-46b8-9cb8-88bb0396960d", "4a47e7fa-0839-4ebd-a87d-b34f9ee7b9fe", "308deeb4-63d4-4614-9a9a-a61ad8bb39a9", "7ee3a33f-fd2c-46b8-a046-686f2b5ad3d8", "3f170d05-860c-4213-92c7-65ae946cdf3f", "6fdfe362-2265-44a3-87fd-bcad659937a5", "274ddf1a-e878-4d99-bce0-ee4d2df28cf9", "748ed4a8-4159-4fe8-8f27-e3ead7122dc5", "f401ec9a-22da-4549-828b-057249a33dbf", "61444a56-f795-4ec2-b53e-e45b8b57586b", "74be45e6-c60e-4251-a024-7802d9764267", "76df9f85-02ed-45d1-a8e8-af2760d5905b", "0557c23b-ef7d-42f2-a64a-4f255070f940", "68eb54f6-8b1f-4fc0-a881-aaeb945aca66", "48417f01-cbf3-469f-9b95-675dcac5bfc4", "b0a1deda-5c5d-438d-892f-134d07919747", "d8200232-a9ed-4682-987b-4bb861c2a23a", "248bcedd-f456-4644-ae2c-33ff80e051d3", "a18921b4-576b-480f-9129-3ad1b1cba51c", "7192300c-1090-45ef-a56e-4c21a8f97c1b", "e787af7f-683e-4532-b8cc-2935475b2928", "277a21dd-9673-435b-8b33-f8fc4faeb937", "eee4ca23-f3dd-411f-ad14-571d9398bc16", "63041708-6de7-47fd-8429-0c812474ca6b", "8738b17f-9cdb-488d-9b8b-3cdec11e14dc", "5838f666-757b-46ef-bd4f-79d2bd70e47c", "2edfbea0-5625-414c-8477-0f6c0790ba1b", "9ef6eecb-adee-46f5-a81f-591f4c931457", "a87718ee-3be1-4239-a998-5c5ad07ff6d4", "a57116d1-9fe5-4266-89bc-c1f744a61e28", "7d992afc-6147-4c2b-8b6e-0e7266240417", "f518b6c0-1259-40e2-807e-e10b6f3e46e8", "8e531a81-37ff-4f52-bbec-15235168f63f", "15b8e1f0-f771-4822-ab8c-0d50df61571d", "9691ad0f-dc9f-4ec1-9598-f3e313ffedad", "d0adb629-ea7e-433d-954a-31494291ef8b", "008d92a0-ef95-4754-bcd9-9156b8b927c7", "adfcc5a4-2f5d-4a7c-8b3e-597d65fda113", "f4ef1c4d-42e8-44f6-8b7c-ce28527a07e6", "0ef2699e-ab2d-44e7-a015-e53126a02c04", "22d4e97c-17e5-4297-a5cc-f4ddb7158b6a", "8ccc67a9-721c-4001-976e-b4e3b0375fba", "89d92418-9ce4-4a41-bd5b-19b6aca7f3be", "2a292001-3aa6-4701-9017-6cc0c384eac1", "ef49e2e5-5c1f-44ce-ae89-3499bae6a572", "2dcacfbc-fa7f-44bc-aa55-0781d3173a72", "43a46321-a1d8-4d1d-ade1-9335091bacb2", "1d1c6c71-8f9a-413c-84aa-177eaaa84d74", "716a52b9-df49-4ae1-9499-59b414faec73", "7025cdc6-9ba0-47ac-a7a1-6eccd3f6b5b9", "7297853e-f39e-432e-aa55-73f141bd593b", "ad423e58-72b8-4e6e-ab4f-f3b62694104b", "ea53c91b-72d6-46ee-9da5-9487f0ff0e07", "c84e5b00-1e01-468a-ae16-05828c63b9ab", "56f7ac46-b6b6-47df-91e0-8056680b8236", "5bdb2867-9efb-4a25-ac84-73d0571ee3d4", "3df1ab5f-0462-408b-a8ff-fdbacc12926d", "5e6633e3-f305-4878-ad16-7b904772c965", "7d5dd205-5c8a-4768-a59c-a5428146a0cb", "be1a13f1-ca3e-435f-89e5-1798dd080071", "b19a3fc4-d491-4e5b-acc7-0bc2370f5360", "4608ebdf-3f09-49ec-9cf4-f9aa0dd08e22", "06e9ee7a-77f6-445a-904b-8ad0a36e19dc", "eb8bc075-2ffd-4497-99c4-fc82da90f375", "a40daa20-3273-4cb2-bcb9-a241106a1c77", "b89df179-8df0-4ad2-a5bf-3f30013dea6e", "babf80bc-37b4-46ae-92b4-d3e64567b727", "7341855b-897d-4a1a-883d-81e3f87516f7", "7d111cc3-efff-42fd-bfa7-6dc2cf855ede", "4795c3e1-177f-4eaa-a24d-72b3061ded6c", "032020dc-b8fe-48fa-b592-9016f380fe6e", "2e481ca1-9ce5-4d3c-a6b5-49d44ec01f95", "ac7d8165-1a41-4432-8ca7-c115143ad99f", "98b0d051-a6dc-4f10-8ce7-5c09d6d656c2", "550852d5-1b1c-4b9e-8ad8-d28340a31a03", "abc7178e-10cb-48f9-8963-40279d668a8f", "cb2060de-2036-4543-ac57-0bf71ace6a0d", "a4b376b6-7ff7-4c87-b680-c2f3e90c4dc3", "d91bd915-c994-4b0d-b16a-2d22dd2eaa14", "ad7ccb5f-0966-4c54-bd9c-73d979b32dd8", "2597f25d-cc90-42bd-8c9c-5fd6534ce0e7", "ebb25c34-7a1d-4ea2-861a-6460551e2a06", "6ca84102-a4d3-41c7-af32-cab400164a47", "dd269b9e-8851-4aee-b4cf-630805237f3c", "d18125ac-f781-4307-a1ef-754fcfbd3aac", "fa987452-2ef5-46a7-898d-a08a0c9cf963", "09a26412-d560-4445-a105-c6ade0624aad", "84016977-7628-4880-871d-97d080cf8d40", "ad71a879-f057-4575-8c70-ac31b2d1c417", "99cc1061-2dd8-40d2-b424-f2aa49f070b7", "78c5fb62-ae50-4553-ad2e-54a4e909f823", "2f064a8c-53cb-4ff4-8811-2c5479a46ebf", "ace69afd-12b9-45ed-a8ae-d4f574132e89", "5a44664a-ccf0-4d37-ad3f-142bd93b5fb2", "18551cce-b288-41a8-a984-8c99c565a093", "1c56e890-0e61-429c-b104-2c886aa5c671", "31578f6b-71b2-47ac-8afe-6ce581a2e596", "c97dd7ee-89bd-48d1-99f1-3cada6b3019f", "20826a68-9e8e-4895-a8d3-6f139acea6e6", "17126e2f-a109-4cfd-a78d-58f15045c859", "a381ce52-42ad-44d5-8a3f-b44b139d91d8", "d8b24200-c21c-4773-8485-f4e8f552a95d", "1a1a4362-99de-4b30-9366-7c1f8e6f2f7a", "58d4f75a-c2b5-47f7-89a5-2e7b465201da", "35615ff4-da5a-4cd1-88c3-199994841487", "6745b8f8-dd4c-4cad-af96-964c2ffd7e46", "d637689a-cf15-43e4-80a8-c827e23a7d45", "77dac35c-300d-4ad8-a05d-b97c8e372e84", "5b3fbff0-25ac-47c0-8046-d710ba5d2cba", "fad42932-ddaf-40ed-9702-5a6400d58607", "8694b53e-71f4-48e4-ad9c-d48aefbbf49b", "3e8ed795-3194-49df-b3c2-beef4fbd2968", "f356b2fb-c561-43fc-b459-97e430b5ecb3", "24fea05d-f5bb-49bb-8523-14cf33ff9d93", "f55a3fad-3ccb-4758-9595-0d6b6168e9fe", "4cfd60ef-d59e-4e4b-9016-2b33a833c621", "9eda92d6-d589-4cd3-8c23-7c1139785933", "3df30e09-5c14-43e8-b7da-86d11457cb23", "eed97321-5dee-4763-a925-9d52b3eaf3de", "c9294f4f-b0de-4212-8115-c492e3f6ec4e", "d676365d-9253-43a3-b171-2e44a79afdac", "41d7d447-b8fa-4250-b66c-b950d9d3ae83", "6a409d76-711c-4f97-a04d-6f195ec295ab", "b60ea8c1-6097-4a90-838e-4cc10566aafd", "a56b3939-b1d5-4764-82b0-4cd3161b9b21", "e55a1411-5a7d-476d-ba89-7eda8f574278", "8270cfd9-e78f-44b4-9919-885e586ec710", "3125a70b-a07f-419e-8f02-c3dabb47000e", "c64bd5ad-34b3-40a1-88f7-f00d015dfae3", "f2ad940d-d39e-4398-9fb1-5d516bddfcbf", "fefd3d7a-91f8-4c31-9b73-22353dff4b47", "ee071071-a584-4bcb-b70a-fd06e764d12a", "809a5a75-4147-432d-a292-5b204494753e", "3f7f0e66-1ed5-4b5f-a10d-52e2a426c453", "e35d1133-bb81-41c9-b66b-eed6f805772b", "b014aa1d-18a5-4c43-be68-41bb74a4323c", "9546c786-4785-4661-8fe6-f085fc5f2fec", "5a49db5a-12b3-4bf4-8039-2669f18ebf38", "ccf4daf5-f13b-467b-b085-4c80439aa329", "5dd9359f-6ce0-4a92-a8c7-21ef67a4070a", "5906566f-b6e2-42b1-bc34-ff657324e014", "3c0c2135-a217-4047-8ebc-d8bdd3e7db7b", "2560655c-7fa1-4278-a7e4-1bef7a834900", "55beeb1c-a5cc-4ff5-9c03-8f7110200acf", "d7ebea3f-a237-41df-b0f8-2039d51ce3ad", "b23fa63f-b67a-4430-9388-4c56434b6759", "e9f4d0bc-4e40-4644-a325-c34f0469d854", "1cf0a14f-7bb0-4310-9fea-6059c8126866", "dfe7b5ff-5834-4761-a7ef-a1345f03e869", "e907165d-5b55-48b3-83fe-d4f215fd279e", "5c9ad38b-a993-4a09-b3a7-953fea68abd2", "3ac888e9-293f-4795-9f62-270065bd6291", "257476cf-ff21-4484-9ea0-935eda009043", "4a18e998-bb4f-4ceb-8b42-7f39874bc574", "587d5c81-2431-4b3a-b9b8-f996ef1a144b", "eebb9d67-b3e4-435e-aee0-cd52678b24d8", "90f5438e-c67d-47e7-8e9c-15a83352504a", "f2b70763-0446-4451-992f-c85275e9a8a5", "8beb6335-04ec-4a34-847f-762b102c55b6", "1ab479c2-210b-4d5e-8d80-14bcffa93068", "00fb9375-781b-45c4-a9da-d0b94a5313e8", "98629496-ad4e-4492-96ec-2b4b4962395c", "10a1f69c-1628-4add-8f1b-dd8a280d643e", "370f3ebf-60fe-4182-a31d-c2ad07f22249", "b0a8c5b6-ec65-4e04-ad64-9e90f7d60936", "c43132b9-7276-40a5-b75a-f190ddf3f675", "c311b3eb-4cee-4ab1-916a-65a6f3e4ef52", "06ee5716-343b-4977-947c-379461c8f877", "61c7bee6-7c28-403c-b04a-1b579ea9b156", "15fa1c23-a9a4-465d-8894-70aabaaae11b", "8f55bb69-9ded-4784-a49f-2be2bf143aee", "788da702-8de5-47a2-86a2-2f9f6e51b97d", "5ae72b56-1258-4d0c-a45a-598f317d0f51", "362d3758-55ac-44d8-b131-acb200ea09bf", "a0279c2b-6cf9-4c35-bd28-b21999eaa4d3", "0e17b99b-1438-49dd-8472-058067571c61", "da3e194c-054b-4516-81e4-c209c7275241", "87a663f9-031f-41b6-8f36-5279b85a60e4", "661e723b-5eef-4555-93bc-bd55955809cb", "5ec515f9-6e1d-4ac8-8821-51e428e31965", "2f1576ce-dd13-4bd5-8faf-9b2aeaf54c7a", "1c356823-2efb-41b8-8f56-bf53c1539f41", "1f1c4ed4-954f-49d3-b22d-d8a39bdf12b0", "f10a8ab4-38d3-4e9b-96c5-66224d4f7f69", "e1bf831c-aaf7-4c58-abd1-a45e383f4283", "78296acd-a796-40bd-9d54-04d05c16a33e", "51e86a28-88df-4bec-84a4-a33b3e476790", "2a6b6146-ea4a-4f00-9494-67fcc4e313c5", "b08e507b-603f-4613-b58e-456b32f8233d", "d77ab2e1-f868-481a-9f1b-4361e5c28af1", "93b36efb-450c-461c-aa25-e19200e4d8ea", "d08b55c1-6c04-4f07-b0e1-1679301bc940", "609488f3-ca5a-4a96-b96d-fcfe5f9234f8", "d8890235-de3b-4b1e-b500-edebfdd8540a", "b5903379-9a20-41e4-8cb5-df6f704740e2", "7f86bea0-f770-479b-aa4f-ce0872adf69a", "ed00fac6-0597-4b6f-b028-6b38d4da9740", "2e138a37-59b1-4726-b000-1eed4788ee79", "f0fe77ea-2377-4b5b-bd22-2a99a4c4bbf6", "3aeeb9c9-4286-46d9-9817-12179ea427a3", "6cbdc9e3-4b3f-48f3-afe0-9b9b9ef18368", "e447f097-2f3a-4893-a020-c2e524e4820b", "69f837b4-3488-4206-be8d-00d5c1d86278", "68b38395-40ad-419a-9488-69f2132733b2", "a1fde378-6b12-4067-9d51-e52ce3071279", "ff3cd8e1-0deb-4e7b-a5ab-fef2b27eb3f2", "fd6269bc-2997-413c-89f4-f6fafbd8a464", "9d98930e-c393-47a3-b219-96f6b3f1a0ab", "9cee9190-1aef-40cb-ad68-2b2085c32cce", "858db7e3-628f-444f-b6c3-f5b4a49bfec1", "aab5c10e-d0ba-4321-a025-f80db1d59601", "10c0d3ce-a20f-47d6-a1ab-12c8494f9343", "db7d98a3-369a-41a0-803f-622ccab5d1c5", "1e70609f-891b-477e-a8bd-dd68511eaf76", "6845687c-c90a-4adb-acbd-50a8e1b009d6", "c1d91c4c-1fb5-4f34-8e5f-f1f2c425df7f", "631bd757-5361-4c2c-8a50-9738bfd35301", "a3fae39f-64e3-4abb-9e8e-26e818f000a5", "91b9d13f-3b2a-47ea-b323-9e1b22eaafd7", "82b64825-c8f1-4693-90f9-97e67cfdeb72", "e7b6558d-0efd-4e36-b2b3-efd4eea04bd9", "d2b26179-3119-4fe1-a852-be13d49f81ff", "7e39c375-10ae-4e1d-9987-b20434892262", "2479a8de-414e-42d9-8f2f-3124d42f6108", "ad4a41a6-23a0-49ee-953c-deef35b09815", "a466cc7c-c4a9-46b0-aa36-eeb61e1dfa0b", "acd06b11-4374-4e9d-a59a-0bbbe535f3bb", "6e6ae214-7513-472b-bba9-1c2098d99646", "0d43c328-e52b-492a-9de9-781d7cdcf1ab", "a37f6913-d38c-4b9b-9b93-723629b8b553", "230dd9e9-a4a2-45af-839c-5c46625da6df", "9897e41a-fcb0-408c-82df-d0e38ea071ce", "8d5daba2-6c50-42d5-8331-a03783fe14e4", "c66af8c3-ad83-47b8-9e5b-dd58230d45aa", "5a205f91-ee8c-4efb-929e-960f88dba7ce", "b9d3bbb9-2b99-4ef0-b674-05f8e01ba375", "bdd7ac24-9494-4fcb-86f3-6f51970481b0", "beb9477f-1c93-4310-aa0d-05e235f578ac", "0ae7b70b-7c05-4ed6-b2fc-0fda487386f7", "3904fafc-8b7f-484a-a6bb-6bf7c86db62a", "8c994929-145a-4874-9408-d0b621d020fe", "660c9685-be59-4d41-b9cf-cb795ed83bc5", "b8fe99c3-4d46-4aeb-b29a-1c8a26027253", "cdf94bae-5bc4-4cc5-b163-0a64c538da89", "fd1f6fd0-cade-4ea3-b5b7-c5cde7ecffd8", "32a0a565-9a2a-4092-b872-a3694c91d251", "164cdc2a-4347-4bfd-9c97-f48176581b64", "720d4a6d-ebc7-4571-b62f-4569abb40467", "a000be4e-cb0b-4758-a123-1a3fafab913e", "c8bb58de-c272-47b9-9d77-f7c2707292c6", "903cca91-5054-4bcf-9ba6-748ac2633881", "4574ed84-76c0-49c5-a208-5156ee571ecf", "32b5b7e5-1728-4d65-a9e3-cbf8af7be3be", "458e3b82-c3b5-4422-a594-c372acfe8a59", "f76e8dde-c810-400c-8429-de6b332686d3", "1e23e66f-59a8-4457-8f2d-4a3272d68c04", "b60fd305-5e2e-4b7b-b8e9-f6cfccb9df13", "55adb260-e86d-4140-8b51-a9789c5d7834", "41f5e0ea-fb3e-4469-9756-c1bd2d5f038c", "5479defa-c3ad-48c9-b712-7cc53030d6d8", "5b8e5e7e-fdd3-4dd2-9272-fbbd8df54312", "100591d6-4f02-4850-a8e2-925cda8684c6", "14ec3b43-c9f6-445c-a57f-79d3904ef085", "1862120c-8f63-4bd9-bcdb-d7e6dee32e14", "a98c7a2d-82c4-4d0a-b8d1-16953194959d", "14060762-fc24-4bbb-9355-6b852b1b5ee9", "88a074d9-3f92-4114-93e9-521d5f2c33ef", "06ce98b4-6941-4654-9cd7-9f33c7ebf44d", "a478517c-4fee-4dda-9a45-c1fb343ffe36", "61c5e3bb-6231-486b-b192-ed33271f983e", "011b4fba-98d0-4151-b2af-ab468a0f2151", "e1adfce9-b0a7-4a58-b3dc-f9c8fa6472b1", "bfeef22f-3a5c-45f4-8665-fa17a9aeec99", "4989ceca-0c61-4633-beb1-e1e78a6374dc", "e66e46bb-ec37-4cc6-bac2-6fe748e7a27f", "8777645f-bdf7-40b1-9610-e6af2c2a055d", "539724d0-d453-4b45-adb5-5a33c7e188b0", "18c971c9-3e4e-445e-bd92-b28e6c6cf525", "5f8dc4a0-5616-4d64-81c6-b004a4a1349d", "4d644853-45de-4ef2-a1f6-f349b27a5ad4", "7d4a799f-bede-4238-9d99-21b955a3bf98", "0cce9cb4-1678-4d42-8dff-5d412c019217", "5b6ce8a9-d337-4962-b4ca-34323b0a7577", "158cf375-be7a-400a-9113-f26bbaae309e", "2c7a132c-da83-4948-abc4-cf4dd4a2619e", "84074bed-a1b6-4045-924a-c75dfffb892f", "2a361a47-cd63-4e55-be23-1afad9cfc9f1", "4ced218a-a1dd-46b4-afba-3f02f5429816", "b7acf85a-6f4e-446b-a103-afa43740d77d", "91064a70-4c69-4d28-a196-f811cb17db62", "4cd91920-98d0-4eb6-a761-20e760709102", "3e39b700-9a66-436f-9e87-697a4844845b", "1c4bbb3c-1823-4f45-9f8a-8d892c45b3e9", "e7eb64cd-45c7-415b-814e-b74ca68f1772", "185b8058-0020-49be-82ab-e846c57d7ac1", "6d858fce-8a4d-4b28-8038-1a8d2e91d314", "c087ee0f-6db4-4967-b34b-5dd481914396", "1cac6ada-b989-425b-af7a-6a7d6cefbcad", "cb08b3e6-a53b-4814-b0c8-d0abf1f838db", "0128a24b-bcb9-4c34-a6b6-e0cb9dd3ad26", "62f4702a-c547-4b80-8a40-6135d345b7c6", "b664a33a-9ecd-4338-a16f-73988a2a67f5", "ac9c9dd6-f3e2-4e46-8a39-6133b30c8f1e", "e102210d-95d8-48b9-b936-be3e9eb77a21", "36b83994-4fdf-44e6-87b6-46db64bdbc8f", "93bb769f-6852-4b14-9009-6c784b907cc0", "e80c0928-af8b-4792-9275-88410c70bf12", "e13896a9-6655-46c4-b8b5-27450122a379", "20b70eee-6120-4ec5-b428-927339b810ff", "3e58dec7-5d2f-4292-bac7-687adf1eb625", "cc2b68ae-ce11-472f-816c-c9811f652d8e", "b1f78284-4346-4398-8cbf-e019884cafa1", "d0e6cf8a-0a56-47f6-865b-8e2aac77d1a2", "7fc47ba2-d01e-4c34-80af-c15ff322d67f", "8717b1af-497e-4f08-8e9e-7ac73a541b01", "071e72a1-3ba9-4b7f-8435-89f53da5e3d4", "056312c2-e009-4bfa-b475-e6f94c457411", "53980ea4-df16-4fc1-9254-0e525fba60e4", "bc20c56f-000a-4002-8ddd-d4dbf217951f", "4951a422-a967-48d2-918a-8af876765111", "fd97b709-4c56-4ad6-9f10-944b49a53710", "684e81a4-800b-4c20-bfdd-85f9aa79a055", "d1457bfe-4830-499b-ac4f-d738557598ad", "0fdbe47e-f18c-4930-afad-31d5438f3900", "2010e56c-d2a4-4ab1-8a04-4fc49e72e8e6", "22b2523a-2e08-44ce-b659-3bc5fd2f942d", "3fb91fc4-6c37-4d23-bc26-96288589e4f8", "958e05d1-df62-46a1-a916-e5b63d118882", "4227cc7b-fdc2-4c22-96f4-0e8c8dbb65fa", "4b1d5cf8-fc81-498a-ac91-a06f2ca98a76", "652fb5cb-fd07-467b-bb33-521b798bb6a7", "961941af-c3ab-4637-9edd-624b32e74a54", "c6a25564-9865-4044-91f2-64aa8a9886d4", "f1484a86-7181-48e6-a3b5-e593efb72ce1", "7051096b-f5fc-45bb-b73f-b638acabddb5", "8588acdf-7064-4bd3-a44f-a9179b1fe30b", "daeddcc0-dfbc-400b-8690-ec08caa4b984", "2910dc93-4783-4561-b219-01b1cb18928e", "5b784a0c-b155-4a66-8fce-9f62d8470cc6", "9ae071af-6f98-45da-9483-21d64ab6e828", "1e73ca9a-06a4-4c21-87b6-9df663e9b288", "016dd13c-484d-47f4-856a-765710b8b7e1", "4ea29398-5283-41da-98a9-1ee99e0c83a1", "f92944b0-0525-4a27-a4d1-95bbbcfac288", "7f526591-fb07-4db7-a030-187d2fc1d3a9", "6f6c84b3-2642-4639-b17b-12c307366e2b", "7b5cf178-85e3-4b0d-a5c4-44a6da4282b7", "540f1290-c1cb-4b53-a223-c50a91ffa0f8", "a4ddbcf8-9a22-4716-8644-78bbd7e9f982", "d5b889f6-1fe8-4b10-99e9-0e7755fb55e4", "db9a14fb-2132-4a14-b7e5-6650ec912195", "d5a04e18-cd79-474b-9d84-c3bfd66dbadc", "6198f436-6b4d-4355-837c-187792b3249c", "20efb7d5-2b3b-47fa-9792-1985e1ed3f0d", "cc39e583-12a4-4b4f-a84a-eae52e9be1e0", "2f48f432-e1bd-4e58-b158-21b10a54d280", "23d968d9-ba47-464b-acea-4a855c6e6b4b", "363e8672-8b1e-4c6b-b788-9263f20bb8d6", "90f2a5e5-925b-490c-b039-feb50085b437", "72e25ea6-e0c3-46fe-a997-be1a32835a06", "98873a66-c7ff-4640-afae-75deb734be23", "99c99fad-4ee5-4d42-ad50-74967ac5a90d", "35712ff6-fb6d-46cb-bcbf-f5469f0a1b84", "571b399d-fd33-4684-8094-1e468498fd71", "2fa46f31-bb15-4f0f-a7f9-15cb3ccb3290", "e551ace9-b10d-47a3-8c60-cdb7ccbb7f89", "fb459b1a-1938-4ecf-8af1-8d6c429d4294", "c4fd6db7-582b-4e01-a40c-0c9ba53cdadc", "843c5430-e53d-4c84-bbd0-c5ae2fdd75c7", "1efb02a6-7535-4726-93f6-fe748df5b00a", "a42f3ce3-6e38-4a4f-b986-33b14ce6c5af", "9badafe3-3b19-4154-9042-c4144ac90f2c", "8838d368-ef59-4719-a597-35713f7f32c3", "231e3479-b543-4e65-a980-9037aa8fff8e", "a10599e1-cc8a-4f0d-a04f-50d96bc2b2f8", "6a25304c-07e9-4455-a084-6e134add9206", "6dfaa354-8c6d-460a-ae45-434cae0b2888", "203c15a4-8e15-453d-9b34-9047b315cc3b", "3b9abf12-b412-4e41-b3e1-5c3e8d249614", "a99819b2-ffc6-43b6-aed7-a03e9c189e87", "96936ca0-bc19-42db-810c-79bd068fd4d7", "5bceea4d-7781-4450-91b4-0d1b602400db", "5a42fa18-c479-4599-8752-a880142ab820", "f62f65c5-8eca-45b5-8469-214a6af64005", "dab4902f-76b0-446b-bd4e-a6a545bf34c3", "710b9d6f-f8d4-4881-a731-a230dbb23c4c", "761f8290-1f87-4743-bb52-ef1161484db8", "793b2d8f-240d-4e39-8bc7-ccbcc3fb8e90", "50adf1be-afc9-4427-a5a0-64af3309277b", "a9452f5d-7be5-4a5c-b3b8-d456f538267f", "6e21794a-48f8-4648-b5e8-e647b831f8f1", "7d893ecc-0487-46f6-badb-85524c996121", "e6450749-7429-4ecf-8c03-acc89fcbfdd1", "4252fbb2-3b21-4138-9051-79e3a043ccd1", "35c97321-10a5-494a-951b-e33b02e02654", "7d598e7b-4db3-4c68-b743-ce1776102d49", "68cc8b40-b9a9-49e5-8e89-e29b09cbe3ad", "2ae2b94e-c532-4caf-9a05-71571c748c6f", "e05b7a20-d842-4321-ae78-4e6de7c4f593", "be97c46f-48c2-40a4-9cac-155afd1bf7bd", "6d5d52cc-bec1-4795-b2d1-99ae0d237368", "adc13271-fd83-4d88-9524-71b99ce758e0", "f193fcd1-a546-46a3-901e-641caf75a369", "d828f6f6-85ce-45d7-8544-d384b64946f9", "761792bf-adde-492b-858b-19a65d3b1ba0", "0d86fb0e-6f2e-4332-8107-2e9b3e9e3261", "5948c836-22da-42a3-bb10-98e0e0c1ebb6", "2f182f2e-4b3f-4e2d-baac-dd720e714eac", "1d204bd3-a655-4fdf-806a-ff07da70d239", "f7e083f1-b179-479b-8d34-f3f6e088c20c", "dc73d638-eea9-47cd-b49d-cc7738965e6c", "5add3f7f-3e0c-47a5-9677-62600c2a9813", "b37da678-90c8-404d-8400-10daa0ee7ce3", "451fef77-734e-4edc-b2d9-19caf30336eb", "d6f143bc-76b1-4a9b-974b-48429d277822", "31eb84bd-1368-4f8b-b407-a7b14af1ebc0", "52ae3c7f-f2e4-4b84-a69c-4725b529add5", "672ad84e-74e6-4e7d-b3b3-4b78fb8d836d", "d407ec32-13d9-4e75-bef1-83ddb1081458", "138d1d35-b487-4730-b8ab-908f5f0ab7c5", "d74551d2-4df0-441b-b3db-18e2cb671137", "1ac0fcac-6c10-45ae-9ff6-fe3c35b978e7", "3e8d93fb-7c89-475b-8d71-a9bf825186e8", "2aefd7ee-e838-49cb-882c-4fb767d7ddc8", "95a370c3-4e24-4568-bd77-f4e2396dafcd", "39050206-5b42-40a4-ac47-3bbb8927b579", "f89d2757-7254-4282-96fb-36f79f37fe95", "ad392566-0dcd-47e1-9e11-856477eac7f3", "5b9360b5-acf9-4c9a-a0dc-37c584eb2eb1", "2b585261-43dd-4476-92b5-c1ea0bfe6b26", "e509778a-a95f-4fa8-b949-ee357157eee5", "1e0c410f-915e-4d32-8231-a88f3e81fc54", "7b30ebc6-ba6d-49a1-b8fa-340cc0ff0441", "958b585e-83ac-44f8-a040-b0caa4baa64d", "411b8727-3adf-4a47-a15c-301f9af110ed", "c832450d-7078-4d94-b366-e1b61a04ba61", "072deeb5-5031-4adc-baaa-ee013ce19183", "054b44fb-9206-468b-91f2-a1351e9bdbcf", "ef8e6871-91ac-488f-8870-5e17bba71f9d", "2f620fa0-c5eb-4b99-8920-72d44a0a5852", "69fc25e3-4dda-4f41-bfd5-a283d5db7bc5", "3a9c0519-db3f-4049-b449-4221eccd2b7b", "9854585a-f9d9-4e51-aaa2-d6bd09f12f87", "d49a80a5-a0d2-4479-a729-d17ab4db4f56", "ba027020-70a2-4670-89ce-2d3a0651eedd", "eea19e25-e5d2-436a-b688-1e9d1536da5d", "df82afbc-ad88-4d25-a43b-9dd34058b77f", "86db9b73-1308-4ee1-a072-ea82236eb42a", "a9fd991f-543e-4fe9-ad7b-dbc3f65e0826", "771abacd-1dc2-4348-ba78-7e1ec41927f2", "51f27a9e-534c-4111-9807-ea5df17bae0b", "d0a57445-da18-4279-8083-c01c0ee38112", "c29116b4-bcc5-48a7-bab4-0d929f512fd0", "238d6819-9d49-4c1d-a025-dadb7418375d", "6dcafa2e-fd80-44ef-aa57-881bb9b4b63c", "584f8cee-2d7b-4418-b8e9-c00010131ec4", "1c31b77a-1ead-4824-ae67-31773de1849a", "42aea103-989e-41f2-8203-f4c6bd3495c2", "687c4bc9-b850-4e38-80de-4f655395c16c", "043320cf-4543-4ddb-b495-539465aeb21a", "3e6a4823-b0ce-4059-8fdd-10b5b98d3962", "c5ac0072-6b5c-472b-a6f3-a6b02a5f2cd5", "206d4d5a-b643-43be-98f1-7448037383a2", "df5dadc9-4fd3-42cf-a365-be3f6009a5bc", "a3e7028c-2eac-4028-a19b-0d29c77ceef7", "c2e68d19-f5c5-46e1-a022-169ece638a7e", "7174b129-7e81-4824-85be-5832f465545b", "545d13d9-0970-48a3-b6a4-d74dbbc970a9", "f73ffd88-8b7b-4995-a551-8b0be4246a9c", "c42dd427-c265-4725-8aec-da3c6e0f655d", "83d0fcf9-d6fd-4058-a338-1309da06778d", "d135273c-1c56-4a0c-82d7-cf00d4b3de53", "27e3456b-2b12-4c0d-9470-935c66d414c6", "55d4319f-f6bd-418e-96e8-c2b46ab7ce33", "8b287459-59bb-424e-a15b-c77bf5168eb2", "4536cbc0-3746-4d6c-ba0b-89f7653509a7", "9a27a8ec-ef98-40aa-ba8f-b2b07f7402d4", "407a028a-681e-43bf-9d9b-056cb768e619", "b82d1a65-f7c7-4491-847d-5335f7f80a26", "692fc027-1c7b-465f-b461-6ff558d91037", "997d59b1-2f98-4b6e-92f1-86545dfdaada", "32121b76-e160-447e-b6cd-6cf5a2b75c86", "8b6bc0ba-edbc-47dd-8514-e409f20cc329", "8e83e015-6714-498d-b637-732a1b203c1a", "ef6f7933-26b5-4dcd-afb4-ed228f4ad674", "d79c5c30-214a-4f5d-9185-73729a73a685", "d53c9816-17f9-42e9-8043-46f3960b01d9", "b171cde2-6a87-403c-9652-7eb866b5229a", "52da850a-862b-4114-a0ae-280f525ea428", "b0c3cbdf-b6cd-4f54-8fc3-72759d011004", "7d22c1f8-ff6b-4185-8a10-f44b8d6744fe", "11ec0c4f-448c-40d7-ba76-15095d731f8e", "f6e1bb25-6447-4ff0-9ebb-486d63d7ab63", "0f316d7b-9631-4137-b7bd-2b3552ce4489", "5d28d936-4311-4846-94f5-8dbdd2045d1f", "a6f373be-0dd8-4fdd-9e7d-4f35d81c42e2", "681a80c4-6534-4284-9cd5-0b870e2a9047", "0f0b1105-536c-40ae-a27f-0cc37eeb094f", "36cb6733-5642-4e24-9363-205732c68fe4", "7a0cc3d9-cda0-47ef-8866-c0019b251e4d", "bead94a7-8d17-4fd2-adbf-5c93cacf7ee5", "f53fee6a-ed1b-4b17-8d42-2c5d459515b5", "6f8bd3a7-bc8c-4a0a-9e88-55a8fc558661", "7042ea1b-f8d7-4695-a8e5-5e514eb7d066", "3cf85df9-a4d7-4570-8705-2382b570c56e", "46a338b9-988d-4ad0-b6f4-b887ca1cc5c4", "50864c42-58ce-48c0-999f-8eb10ff4430c", "5b9823b1-cce8-4b89-966f-5deaeac8260e", "56c2552f-31ac-43d8-b97d-c3c32dd4fedf", "6157918c-3b92-4df1-9a47-e25d75a8e05b", "ffc9b303-5eb4-44b8-98a0-846084f993ee", "1fc09828-38e7-4c72-bf74-cdf65ae53c90", "2b8fba94-4477-49ef-81e3-bc4717564c27", "b64e4c06-0178-4afe-946b-0729e2fb1724", "2c340bba-65e5-43f7-aefc-4c41a66ea236", "cb3d8d05-acac-4ea9-b2e1-837c2a2b846f", "208f3900-2b76-4569-823a-4ff9f362060a", "60f79f1e-5fcd-4b94-b570-a8b124bcbf35", "95a69139-7358-4b12-9cdb-5c842063c567", "98090b07-2b33-4f31-a28a-88fb0af91cb0", "987df8d4-227c-4412-8178-ef8bfec8a819", "325207f0-402e-45c4-ab96-85553ec57016", "bd928d37-79fa-419f-a0b6-356139333aca", "de5da539-c8cd-4519-ad69-1745f822e0b3", "445194e6-2a47-4e97-a85a-70df440c3521", "f41bbfbc-2283-45f1-bd83-1d844b034ae2", "ba94a17f-e348-4c87-906c-186c5c1398bd", "9ef2ba29-6414-4e44-bd79-3497b5bf2956", "a47c3de8-6d44-4f88-bef9-d207ed6167a9", "8b14b76b-185d-41f0-82a0-c1b8cb3ee8d6", "db3d6038-e851-4960-871b-74f5095e481b", "cdf0cef8-db49-4f02-8688-ab7ddedaa534", "7442a96a-5af3-47c6-ace0-869358cd051c", "3e91764b-ac31-4a3a-b6b3-6bdb6cda00e0", "381f12f2-b973-436f-8376-b0084218a8de", "92ebf849-d2e6-4732-b557-c77669746aa5", "3826cf48-cc6c-46e1-840d-f800886f7a4b", "e9080098-7157-4485-9970-edefd1bcc551", "341ee390-1f66-4d0a-82f8-f72bebf5d551", "f3f9fd40-6218-47d9-be1b-ceef2a121548", "a599b710-aeba-4e65-a7cd-890948b1a644", "de21b7de-916a-49a3-ab7d-53489bdc48bb", "6e75bb6e-7bd2-4f0b-8d83-fbecbca3bc88", "93e70af5-1938-4ed6-8a85-fed95c800d0c", "00cd3965-d554-434f-bd92-3632615e3f4a", "7892713a-6331-44f1-bc0f-24edc16a7c16", "730014e9-5d67-4914-b39a-f611d41a0f6c", "d962ae5e-06cd-4bf8-a2db-152d2cccf85b", "0cb87d62-4cb9-42c0-8781-4334c7a62719", "08eac5c8-5c73-4c8e-b591-3e6da7a9aab1", "ddb855a7-97e7-4cf7-9542-20e3a1a48e83", "39c84b42-5447-43be-bc55-75e557196496", "495af3ba-f821-4516-ace9-4d532c80863c", "fa07da1e-1293-4e5f-b6b8-ddd5f421cc1a", "5e9717c5-7459-4fcc-b3b9-ef07bddf6836", "50a18acf-24c3-4c37-96d5-5b48ab4a1fb4", "90b67d66-c960-4341-b9ea-0e2a7be849da", "37e24d71-e318-452a-8780-e099cb36c20f", "a7d0f997-4c45-40c3-ad89-0b4168486b98", "7cd47112-bac4-45b2-8564-f1977a7e11e3", "abce0ed3-5dd2-44dd-acb5-f524443b0c06", "3a8b63ae-d9a2-4ecb-950a-6f04dc7f16be", "2bef197e-4b8c-483e-8020-d65d24772b4c", "62121c40-cc96-40a0-b93a-beeace0fb11a", "efda9f3e-6248-46e3-aa3c-09f6a3b2f457", "698c8b51-b68a-447e-bab2-5ab609bfe063", "03d09de8-f13c-4576-a359-e071b7947817", "c6523fcc-8531-4f11-8b84-4191900bb0d1", "f13ef3a5-3e11-4bad-912b-634577a8d26f", "e2c08556-ad3e-481c-a816-4c3f487f1872", "20d31883-6028-4a0d-9eda-f931eed8c8ca", "f3957965-e9b9-4696-ab77-6ddcc1be0e24", "101cd06d-43a4-46fe-9a6e-6ef67e510762", "ae556c95-1ceb-4a23-b5db-887de879baa7", "176b4f71-0b82-4b2d-9d85-494f15919c1d", "edad1b25-f353-4cad-bd12-29e89694deff", "a9e6e9bd-7b8f-41ab-99d3-4bccfb9f320f", "4ee69320-4aef-4e43-9607-de2792b6beaf", "293f91de-d031-48cf-b66e-9b5fb2a03999", "69be4ae1-13cd-4841-ab81-5c6f6e5c9bdb", "e4a065dc-b8bc-488a-bec6-16e2788111e0", "e23b7fe9-30ca-4b66-94dc-36a360503bc9", "798ea6df-b452-4144-970c-934c7e9c17a1", "ae4c63df-3e1f-4756-9a02-daf5718aeea0", "1b9c6405-0b87-42bc-9963-29c0a205fa93", "e169dfe6-4348-4bc3-aa49-54d49962aa26", "858de1dc-438f-40bd-97a0-d5404cdf9718", "ca2ed152-6a1c-4010-8f7b-304d17ae672b", "d8318218-58bc-4ecc-9bfe-a0bcb0510f3f", "01a70e4e-fc5e-450f-8a58-aceccd8dcd7c", "472d81a4-5deb-4a76-bc25-c58f5007b197", "3a27948f-94f1-48e6-ba27-ecebf43d1412", "de962137-4f98-4da6-b2c1-6f7c27934226", "1e2108f3-4a73-41ef-b44b-440d73806cab", "8fe2113a-902f-4fcf-8e4e-48729dc6a823", "8e03ddf1-2452-493d-ad75-94702a68274e", "9283071b-d026-4ec3-bfc2-32674239758c", "249131f1-ce47-4601-8fb9-9880005801ec", "dd985874-471e-4c0b-bd1d-d52024a6262a", "dcbc25ab-4efb-42ac-a812-7b84cb327d34", "ce2b7d8a-81cb-45eb-9b1e-9b8a7ecda271", "cf4bff35-b7c4-4ad9-a150-bcb5fbc9f2b8", "e31af171-07b2-4d2b-8663-9512ebe0eaf5", "1e04791b-adc0-47ed-9bf7-81ac68b5b161", "1bc231ba-73ad-4d65-baf4-7cff84ceb348", "f462142a-6a15-4311-a839-49560313f0fc", "b437391d-bd31-472a-950d-5bcc94afcbb5", "881c7dcb-971a-4ce4-a7c9-824dae4cb67b", "b73f2ee5-3c0b-491a-aa3a-052c773e7851", "c19485c0-1409-4dd4-9d84-8d730250f6ec", "8e2a47e8-01c0-4138-8517-dadcfd6ea81d", "8c353aff-71b5-4f26-b086-50988136b5b3", "b1b13bed-51cc-4563-97b3-9fecc6d42381", "40af28f2-5a12-4e94-9866-2d26509da7e6", "055caa4e-bb53-485c-9c2a-7dc2b4755f2c", "fa5a0306-9bc8-429a-a33b-ec3c69376e79", "11149e51-4240-44ad-a70b-20f063628d44", "ff4e5ae5-c281-46ed-ab7d-f3cfcc9a334b", "7348afdd-52c8-4eb8-9bfe-37e26addb22c", "a100b0a9-a406-48dd-ba9b-632708151d90", "e951aaa0-7767-4419-8e64-e9c6385000af", "550592bf-e806-4cc8-a6d0-99b483bbfa8a", "380c5802-6fbf-4220-a795-3b537a6070a2", "e57cb4a4-e785-4085-abdf-b77e60f518f6", "1f443f2b-f4de-4d66-8f0a-202fe321a3f9", "de7db18b-a594-445e-88e1-0f0e2d74e7ef", "384e8fc6-7ed3-479c-b106-92ddd41b60d3", "a77faa5c-0415-4011-b146-d693329d5f4f", "68f77884-f2e2-4a1c-9751-2acdf15360d7", "0cb29cf1-ba8f-4afd-b26d-a8a4d7a395a7", "dd773a4e-41c5-4fa4-b7a4-2040b1343edb", "c9cbf3c1-6c3c-4700-afe1-c7b466418eff", "c00e4dc9-cf52-412a-85a5-631a9847af38", "5f4d0d4c-d58e-451b-9048-20ba8204a111", "8feba034-46fd-4058-9b49-7648c2cdfbfa", "b8efd261-36d3-4c38-a8f8-969476f66c3e", "7f1da65f-c4a8-4af8-a38a-218b5ff5c5f5", "00a21d34-3e11-4ba6-97e9-93c80d7834f5", "c2f468df-1905-448f-80db-878e94ea41ef", "a2f8c4a0-f129-4836-b7a3-bc681ab3c227", "bee40cbd-394c-4e10-9d51-53131a55dc20", "d9b81993-67f5-445d-bc49-55396b93d9f5", "92f7d35e-3371-4323-a533-13e3ec24da34", "86478ff8-d81e-4f5b-907f-23fb3978c5fc", "c9adeeae-458d-433c-ad59-33f04a6d51a6", "c0712225-8de2-4900-90c5-aee380b65b9e", "5d54662c-a071-43df-ad20-4bd150a4cb9b", "bc2ea325-78dd-43f9-9ae6-9b953e31a557", "153087de-b79c-4974-a7dd-378c37053b8b", "3d0fd9e6-559f-4291-996b-b898c03d32ca", "d31601f2-6e3e-48dc-b41a-6070f1f2f9bb", "4f75616f-8100-4b90-8447-147eb0fd626e", "4ccfcddd-0453-4cc7-9c70-88e133462310", "a4ad54d6-9004-4fee-93dd-e35d759ea5ca", "b9e786f9-565d-4332-8ef4-836800850950", "943cff80-0655-4d42-8ea0-e042f9296744", "7302a062-ca6f-45f5-b0cd-5bfe259ecfbd", "b679a09b-58a8-4fb7-ba5d-c09d81c51ee0", "88cbcb50-32d4-454d-9498-80bc4f05e261", "4286319b-6259-4350-94c6-d32086cb1f8b", "a3b054ce-8d3e-49d4-9171-6eb986043f17", "520c814c-dde1-45a9-ade5-f40f2a3f7913", "5a7f3f67-7c13-4c1c-852d-5515e854518c", "a1b4977b-cdd7-431b-a18c-8179bbf668e0", "31289889-ca56-415a-87b0-c3457a382833", "7ef6b3f7-e118-48cb-949d-995f0980d713", "ee955a32-f931-459c-88d6-e1e40b7584ed", "f19b20fa-e5c0-436a-af93-382edf768f47", "9cfe756f-5854-4506-9624-90f13e46aca8", "eaa96547-fc82-475d-aace-5bb50115f277", "f7870250-6917-4803-a06a-83b77afa83fe", "cb14c5ae-b764-41b9-846d-bddaec4a406b", "ffacca30-ba96-44de-9630-18f6de45946a", "ed650a10-735a-4523-b0a7-c1e34e8e389b", "f6fd869b-42db-4254-a947-4437bd3b8610", "2abd50c4-cd11-46a5-bd9e-08d0b78a4a83", "d506d9d2-fcda-45cd-9aae-b63ec45040cd", "ef6b0fee-3786-4373-83a0-94f3a4171b2b", "b219ee71-dacf-4493-85e1-24648a09473c", "5f2daeaf-891a-4a35-9ca0-a95087d597a5", "4779f837-0aa1-4dd9-8040-51a7f506d3b9", "6c436936-c5cd-435b-8385-4e5bc15e0a85", "3c09b1b0-0c0f-46a0-9cc4-877707c69a93", "3c86efc7-653a-432e-b9bd-e6735b30f257", "255816c9-bdb0-471a-8467-aa43ef5e640e", "e287dc3d-af11-4ebf-ace2-0c37acbb4f50", "217b23e9-b10f-403d-ab3d-114da589cc2c", "80781b96-1760-4c12-9971-d600eb554681", "68da0c12-7401-48b8-8280-814f907601db", "abb91498-e265-4fde-86c0-e0fb9ed41af8", "6a7d6362-ad12-4ced-b08d-f74fe352ffcc", "f1f9493a-185d-4826-b383-6cafcdf91d9c", "be0dbdd3-1355-4ff6-99ee-539c822018b2", "31d8d2b3-1ae9-41d7-90b7-12c49dc4106d", "effb1dd0-54a1-4ab7-a822-b883326e2474", "dddbd7dc-77a9-46cb-97c6-d9934ce7c856", "d7eaba39-2d88-414c-b1d1-efe04164ad91", "acadcce3-6eb5-4f59-aeba-af5fd244621c", "ba0012f1-8f72-420f-8710-f362592e7780", "a30b5de7-e533-4fe5-83d5-2758da863293", "486928d1-b475-4d43-81fd-a4b3a9633526", "0a9bb297-6e0f-475b-94f3-6f20ba29db57", "2931d5a5-c38f-437a-9e6d-aeaf362d2517", "30e02c8d-3305-43b8-8b3e-2ff741c378e4", "63329501-ea07-416f-b1f7-e5ba80f15091", "da2f9f4f-dd7e-45f0-90b9-3c2d17b121e8", "df3387a2-ee78-420c-beff-8729a61895c4", "e288cdea-f34c-4ec4-9393-5fef1505b47e", "7dbdc4e9-2502-4edb-b503-256ba625b1c0", "cfdda691-6d99-40eb-b30d-01e38cf7a07d", "6679f53f-9c17-4186-a2ae-757a2b5d3229", "b5a0d22d-6113-4c16-9f5a-efe71208218f", "07ea7433-bb4e-476c-8fe4-cdf3098f1676", "dc9c0273-af63-4ffc-8966-2b117a1a7cb0", "b149e2e9-9185-4d2f-b62b-de52da434b06", "26caad5a-62ba-4d42-bc3a-4debc64fa914", "c18e0c52-671f-4b28-9761-e003afa4ec1d", "9f7713c4-92a4-4dcc-acce-ce0a1b7dc722", "71400e9e-035d-4295-a3b2-49c927d180d8", "7a496403-b9de-409d-a4ea-9ec54868e7e3", "3fbd0fe7-a5aa-4160-85b5-6ef0077458c0", "c5ce9c99-f26b-4cc7-b251-ff32b61ade55", "0517b6bf-95cc-41ef-b649-98f4d28ec470", "4af7f3b5-501a-451b-83a5-ce291152df4b", "8cdf698b-505e-4e0d-b047-c4525e9e898a", "4d926e26-0bd3-45b0-8d0e-684cea2f7851", "8a8bea13-ee82-4b75-aa0a-8770ee54d1a5", "57d114d3-941d-4db7-bd6f-7a7075a2ab64", "a2e6fcf5-65fd-4fd2-b39e-6367aeabd351", "accf3833-afcb-4591-970b-441622fa2295", "1964ed37-39c7-4aaa-ac76-cd716a3a060d", "8bf7b17e-91f0-4e7b-bafc-3e7072e7e788", "74a2ccd5-ca7f-4a9d-a177-baf00e609d23", "eee0d8bb-100e-4cc3-8f35-c9c25c18f505", "4172b474-7aec-4ffd-a278-49b98a08051f", "9a90034d-900c-451e-9982-0ca013252899", "444aea61-edb7-4f3a-ae2c-113e42ab5ea8", "b84c0e31-83ee-467c-879f-17d8ec66ea44", "9d0528d7-c191-4e28-ad2b-378c7f3a6e72", "d4e3a83f-9373-4839-9ce5-705d4a33b94a", "8aa43164-22b5-4270-ba5d-dcb5b4a047f0", "32ddf646-bc96-4c66-b9d9-df4b773448ef", "294bce0a-a596-4812-875f-5e34e8a5833a", "87aef4e8-24da-46ce-85f2-1cab2e6b2607", "ed30be70-f742-45ad-9633-ad0d0adbce52", "dc98c4cc-1c55-4d68-99d5-041df0f60431", "2d88d28e-4d42-421b-a632-1e947787a38d", "57c6de38-4654-4345-84c7-9b048196f9d7", "04b90322-01a0-4859-8f1a-4d68e2b20fa2", "1c71e397-7c73-4b18-98d2-a9a9c5a37c57", "b09e181d-1f3c-47dc-96a7-fb2f510455e9", "531bab49-dd04-4233-8ef2-759187484114", "974cec85-c628-43a3-a7b7-46d197b92501", "03f77a9b-868f-4ef1-84d8-51d7696a6b3a", "c8387668-53c2-4b33-89df-d7249e48d253", "57117c27-7237-41d9-a293-bf7d5c03e607", "ccc090ff-8970-4adf-a4a8-5a11739c0466", "2e268e19-79c7-43f8-ae29-86138c6e50af", "b2ddccbd-eb8b-4bb0-9d69-242967528337", "26525051-fc8c-4e78-8c0a-a04f2c1ba2d6", "fdf2852d-c8ac-4001-af5e-383bc5304e03", "59c5e21f-a32c-4949-adb3-f3d90a5fd249", "f11d22ab-ef8b-4430-8e54-f06b0f2f8957", "bb907daf-fce2-4d88-b0b6-b3efa8637943", "ece4b52e-671e-4b0c-b543-3b1cba8a94b4", "ac10489e-78d6-41b9-b45f-c5ffd02e590d", "4a2dfadf-c898-45b6-a5f4-edf67c23c2c5", "c3b5bdde-60d2-4c9c-96f0-aeadc744bf29", "33edc72c-e3fe-48c2-99a6-01750f211e73", "3eb28dd1-1170-4394-af1c-42aa978583a3", "ff7029d9-815f-4c3f-92e7-2cf54a66eb9b", "38fee070-5896-4645-8900-a9787ef1153c", "667f2b4b-9245-4a75-9abc-2374b57eb333", "99e90e1f-527f-4360-aff9-0731a74784a5", "a1245e00-7483-43f5-b36a-457a01262fce", "fcfc0b5f-45f3-42d0-a258-479794fcaf22", "ea647e57-a891-4d0b-9750-c3b5e772ccc4", "a87b05aa-0cdb-4797-94f6-4cfae4efa62c", "f92f7d88-6d36-4d69-b289-a1a25375683e", "7c09d832-049b-4bb8-8980-0e70dd05d2d2", "90bd1657-6f4c-4ed6-a2be-5c3a9151e420", "e40bb338-ee44-434a-84d5-a8d712dc0200", "1a0dda3c-2e1f-4af8-aa9b-635f4a32baad", "62ea744e-b82f-44fd-9a71-31613f77eb41", "256eb33f-7b10-47c2-9aaf-9c058fdcf7fc", "5d2c38e4-32d5-4837-bc62-dafab30bbd89", "8e376f0d-4ca7-4e62-9db5-46b35947015a", "7ab354ea-d665-4a5c-b2c7-cf737e96ec8a", "ab88eb07-f36d-486b-8ff0-3f7933c71915", "ebacd260-d766-488e-b26d-de4feb738026", "2e08a007-2a8f-4abb-b2f1-b01d67249e06", "5a85bfbf-fd67-4f68-8f29-4aeddf2e51dd", "bb1f9204-24b8-4692-80b9-e5fd16de33b5", "a20702c4-f72d-4962-b0b5-1fb1dff64088", "473dec6b-800e-419a-bb55-dd18437350c8", "7c749003-5854-4062-ad95-0112546861cd", "1379aa92-b54f-433f-9ddd-6096f817e7d4", "e6a601e7-096e-4aae-9045-58b04c1c49db", "68becc5a-3a28-4265-8ec5-7ea5b2cb3e25", "ad758db7-b3fb-4f12-942e-a611c1e051fc", "654a908e-d01e-424d-b80f-b17d5090a20c", "271ecb91-5891-4493-836c-a0ed111f0ba6", "771762d1-1eb3-4989-b9e4-d990fde19533", "f1ff993d-7503-402a-aa24-b71d2183ac32", "b86bfb17-74e6-4782-b095-c7f679add192", "1da1a748-abb1-4c43-ad8b-03bc3668e061", "dd8dce4c-efe0-4022-9f8d-01799ee65c0e", "c6d6da03-934c-4e22-9116-9d4b08e1c9d8", "e281c0cf-5824-4cb8-b28b-d4ebcda135cd", "e7c07873-ecf2-446d-aabf-c731ef383644", "241ba196-2543-47d3-b4db-d23059f90a9e", "fa4e5ecf-b463-4b64-b6c9-88afc0cc491a", "930eea42-3dc0-408f-a07f-8d2f6f0d707a", "f54acc38-640c-4fd9-b7a4-a562b6397e6b", "a9347718-9830-4108-929f-99296f20f464", "832de106-ba28-4755-b86a-13851f13853f", "4573d26b-7995-4b15-9a20-fc050536ca31", "b7ca5267-b372-450c-b53c-0fb34b7b21ed", "d53bdf5d-52b5-4782-9d26-9bae2a49d950", "a8c51a07-d7e9-4be0-8d3e-a64a91000389", "72a5bd26-fc2d-422b-850f-45c18c6a9ad9", "2228c6f9-c4ed-4761-890f-18f65850dc55", "a5bb4b06-ceb4-44a6-b257-e24692db8584", "fa48ea96-c63e-444f-9266-1754fcab0a98", "1d4aefed-6129-44fb-8918-0763954c2feb", "3a18bd1a-c2cb-4103-8b4a-0562e263e8e7", "7c75218a-223d-4cb4-a327-7110113e9f38", "030ac3ba-836f-4ecb-94ce-050f8b62fba8", "298815dc-96b1-402f-b180-5e5a1f6ec4fe", "78ff0e14-9dcf-4ef7-b91a-f826f98c773d", "c1203048-4e6c-40ef-bb83-3b1b127e93df", "b4255740-9516-4071-a4e5-05e35decc13b", "22a4a0dd-7e02-4370-901d-4557db458f90", "c0c7c8c7-ec06-440c-b751-4ce97b514b42", "4f2c8dc9-9207-4dfc-ac16-34d4f46674e4", "0caa0366-a849-49bc-8b6e-6b8c52e16b66", "c7ae197a-8459-43d0-97f6-07c451754d57", "42629110-d99c-4d21-8c8e-cdb579f7a162", "fddf7578-1faf-43b6-9cbf-85a61883e871", "aae514fc-7785-4155-8c51-08ee665bbe28", "3ba7e046-9555-4ef6-b0f1-5c3f4f87bb0f", "8e4b0b0e-02d5-4112-af07-3eb9e88f49c4", "f67ae270-119f-40f4-99cd-4050fd4714aa", "bb0ca380-8bb0-46da-976e-31a782b5a56a", "950e36e4-8d00-4e2c-a863-bcacb53201bf", "10720714-f2e4-46d2-b0f8-1c6629d7eab8", "16e6f585-e5d5-4563-8978-ead1e82943d2", "5f54c790-d002-4df0-94b0-edea0630eaec", "5508cf96-5a91-4663-bc26-0cdb3f1e73da", "8e2bfae5-ec10-482a-8b85-758cf31a20d9", "45e310ea-3588-446c-8678-8f0bf5a9cf69", "f53cd650-0ed8-48b3-b909-7c3617c8ebf5", "f76d5c8a-e3e1-43eb-9df5-158e7fac6c8c", "e98b42bd-c62d-4f05-8bc3-574735d5c2e7", "c3b9daaa-ca08-44c2-b204-052b7143bde8", "8918e9b9-bcbb-472f-8835-30ed7b8da723", "92b3affb-3e85-4615-84b9-22479aadcad2", "39631d0e-585f-455e-90ad-8c189094edae", "5706c0f1-7687-471b-8834-12cdc6a79a96", "d53c6dd2-e290-485b-9135-a43aa504f4a3", "1c449660-8c80-491a-a70f-cbae6f61953f", "2b7e1e74-567a-478a-b42d-eb9ab4de0f1a", "4ba737d1-221b-476c-a4fc-06442fa57566", "a55e3bcc-1add-4433-b09c-edfc21e689ec", "a530b9e3-ccc0-4924-8ab1-ff93b44005ab", "06dd0e71-0660-467b-b0a9-76a638a4bc57", "5be34c91-d147-4ef9-b5d3-264845c71d32", "5fa3e26a-e5ce-460a-89ff-b53e8efdf946", "be013d86-203e-4780-8a53-e66b37a0b308", "eb5a867f-e775-4cf2-94a2-2e0661e4d271", "ef56cfd2-1c5b-41d3-b8ab-9d37c00bafc1", "a9ec97dd-4719-404c-9ca9-d831aed24dc0", "584ebd93-7737-428c-a099-91be45dbde45", "0b937a14-1026-43cf-bb80-29f5d3166a76", "340005ce-2bd8-4754-bb12-9f720130b36c", "282cd9e3-ace3-48ad-aff4-786e561ede72", "4f4657f0-7ebf-45c9-9e60-2da501c832fb", "1f947390-1126-4d8a-a143-8859441437e9", "e2376750-fe74-44b1-adb0-3cb21a4da4d4", "857bf6d3-428f-4402-98eb-09cd76baad51", "c7158977-52e7-4a23-bb3e-8afba9b154c1", "571d3b75-e49d-4641-ad34-e92cf13a58c7", "557ec6e6-9878-4a9b-a30b-22b1e95dd521", "fc98473e-b735-42ce-b17a-d8f6e137ad0e", "d586ad51-a2e8-4db3-a545-22c160b48ff7", "df3a0ba7-1d2d-4ae2-9269-03a9cca0d2de", "fd4df463-bcdb-48e4-9a5f-ac3952dcc73e", "86c1cc4b-da08-4cfa-a870-a7cbf76e04ca", "d3148870-47e0-4005-bf88-944ccada809d", "0cf8c9f5-0130-4017-a039-f0614367445f", "0d9a5d08-8e8f-410d-81f3-63c60c75e4f9", "835041e5-7539-4744-b0b8-2cd81b0f580c", "d917177d-5d97-4a0b-ae2f-cd1723686ff5", "81971934-8ee3-44fa-9238-9d8e6a71568d", "a3def130-be6f-4503-b590-308d7e97a9b4", "019fb4c4-cf89-4774-8093-680c21ddf028", "7b002781-2f18-4a81-bea1-1757328c3be4", "1dc0e084-6771-4474-a29f-cb75217d46e8", "7732dcf1-613e-4285-ad9b-ded7a9c6a242", "cd57390a-6231-4557-b974-a6692e665558", "2a6a7c23-41e2-4786-b579-5b02abef92cb", "0713ac0e-b9af-4ba8-8f69-a909387c44a1", "f262eaac-7d3a-4c66-8728-7c98a0d5c850", "b8aff062-41f9-4b04-bf1f-33cf869d9b4c", "b70f4f9f-d18f-4962-898d-88c59234894b", "05a50b3e-05dc-4d51-82d4-4efaea030385", "57761e45-8e01-49a3-bc22-47eafcf07a1c", "fbf29ca3-c733-4617-bf33-c8ce2ccc8ac6", "cea8f470-2582-46f1-bbdc-826fa055dc49", "50eb171d-5d93-4c07-85a0-d9641c651640", "ec53a1dc-ae01-4ede-8ec6-ba500c09b916", "d984d581-8658-46eb-8fe9-161d02c4209a", "97457030-9233-49c6-bc61-7b4513eb7fee", "08298f0b-350d-4199-be1c-ac1a9d76d717", "6c6ebfd4-2ec9-4941-95b4-5c33e93f2303", "58806260-0d34-4d52-9759-92276ca9af36", "2737f630-6f7c-4010-9bc6-edcdcf73522e", "ed258203-95ed-43a9-bf02-18e029804d82", "7e4b207e-1b41-4769-96a3-02188e19e3f9", "5b572fde-e8b7-4cce-9836-3ea5fefa09dc", "cd02e5af-4e86-419e-86e9-ecd418a0470e", "2c3f94e9-86b0-4410-b49f-db244d9a6ce9", "b77e0c73-7e68-4042-8fcc-571376eeaff9" })
            //    ReadUserDetails(v, skip:true);
            //ReadAllUserDetails();


            //foreach (var v in new List<string>{ "1ad1cdc0-3ab8-4bbc-aff4-b7f100ae7f9c","06a33dc3-0f28-4a5a-a1a4-0db050153a60","1e70609f-891b-477e-a8bd-dd68511eaf76","10a1f69c-1628-4add-8f1b-dd8a280d643e","1e70609f-891b-477e-a8bd-dd68511eaf76","eb96e4f0-2f35-4ed6-9c21-f038e4f77927","4bf832d8-3784-43ff-8320-1da52888125a","ed4e0228-7006-4c90-8db1-7dcabb25d7d1","22f28122-248e-4a35-bedc-54221ae0a4e5","3fc86326-1778-4289-9215-03d536f6be4e","49c5f834-61e0-481a-9928-75cdcb5cce00","f751e73a-197a-4c0f-9bbd-959b8fe2c542","ef8acbd0-1614-43ae-bf43-751c1de69463","8e531a81-37ff-4f52-bbec-15235168f63f","cdcb11bb-0e3d-45f5-9860-d43c2cd8cf8a","388ea970-46be-4fb6-a711-775976682f83","f92b83d9-ea09-4871-8a9e-c1da588f1cfc","c6c7b913-a729-4537-b105-5bb50169525d","dcdb1c84-0eff-4369-8f94-d7278045ba5d","b3587e9d-0f08-42e5-8d65-92b29c6781cf","4226329f-9fa7-4597-844b-a7f01e5bf66e","31f6e897-b0e4-4bc7-82fb-9f4593198ffd","7b01ea86-229a-457d-8af7-44459b9b170b","d3363d7c-087c-4a4b-8fff-95c779b0815f","99e45e66-57ee-4ad3-a40d-1f02974898f1","35f369eb-0cb1-44f9-9934-7a902ff7bf34","986ec690-5e4a-4b0b-b9a3-b2fca627bfb4","dd7f482e-e180-4846-9570-0f54fd25ffbd","dcdb1c84-0eff-4369-8f94-d7278045ba5d","f7aded5a-05a7-491d-94a6-1756d454d3ba","1e70609f-891b-477e-a8bd-dd68511eaf76","0a9cbba7-c66a-4ac5-80a5-6f5c4fd735f8","15275572-bd30-48c2-85f2-f4607e6b99e4","c70a72bc-187e-41d5-93a7-888420d3ade5","cb64fe85-d0ae-4372-8aaf-7a5f80ad448a","71847c37-69f2-4f01-9b8d-1895d4699b98","4226329f-9fa7-4597-844b-a7f01e5bf66e","71847c37-69f2-4f01-9b8d-1895d4699b98","1e70609f-891b-477e-a8bd-dd68511eaf76","10a1f69c-1628-4add-8f1b-dd8a280d643e","0bb9a56b-8f06-4316-a972-36b3ba2d55ee","183e8053-8287-45a5-8c7a-4c88da76797c","7dde6604-5252-4137-9054-bc82a433fb08","587d5c81-2431-4b3a-b9b8-f996ef1a144b","52211a9e-de91-4168-b697-c787a63c2874","b800091e-8c41-435b-a141-d15b34b1040f","0c26d70e-e5b1-4673-a371-262ff094517d","06a33dc3-0f28-4a5a-a1a4-0db050153a60","b501133f-0bae-4a51-a8e5-6b613f3be5ef","c1052acf-c8c8-4d7b-bbf5-cd7131c9cbcb","f92b83d9-ea09-4871-8a9e-c1da588f1cfc","1209b7ed-47cc-4efb-a0ee-d8cb62600d08","1e70609f-891b-477e-a8bd-dd68511eaf76","10e0e29d-7d2f-4516-8d51-581ad9ccedb5","f09532fd-0731-41fb-b0a1-e8d13039560c","dd7f482e-e180-4846-9570-0f54fd25ffbd","15275572-bd30-48c2-85f2-f4607e6b99e4","33e92193-878e-4fbb-852c-a4a797b05ce4","ee47bbea-c562-4d45-bb8f-0c45205b5c20","1865b9f1-6b99-4109-9824-a9a3b0720381","21caf6e5-79c6-494e-bd6c-a44f4533eaad","02d9d47f-77b0-4e0d-8ef0-c5d1d9076526","751c2eca-3952-4fee-934a-3337b70bc6d6","cb64fe85-d0ae-4372-8aaf-7a5f80ad448a","dcdb1c84-0eff-4369-8f94-d7278045ba5d","b3587e9d-0f08-42e5-8d65-92b29c6781cf","b041a24c-24d4-4184-9bb5-c253fbfc21ca","b34e3f33-7593-4f57-b9f9-c337af53196c","f4fa9204-66db-4b12-a96f-33f4a3427962","c1052acf-c8c8-4d7b-bbf5-cd7131c9cbcb","1e70609f-891b-477e-a8bd-dd68511eaf76","c5105ae7-eb08-4a09-af6c-341d840fb8df","e0275ab1-98d3-45dd-8e3c-7b2b55ff777a","6c175125-d689-4e19-8949-690552fe1223","2a36a36d-5119-42b5-88e7-e24cfed878fd","1d680a89-9dc0-4cb6-9b17-b70be20bc1dd","b501133f-0bae-4a51-a8e5-6b613f3be5ef","c1052acf-c8c8-4d7b-bbf5-cd7131c9cbcb","1e70609f-891b-477e-a8bd-dd68511eaf76","10e0e29d-7d2f-4516-8d51-581ad9ccedb5","670f9346-91d0-4fee-bacd-5815d6062874","f944124d-f30c-43b3-ac49-f8413f965b8a","7b01ea86-229a-457d-8af7-44459b9b170b","b800091e-8c41-435b-a141-d15b34b1040f","ef7038e8-8d4a-4540-8a78-afc6b53cb81e","c70a72bc-187e-41d5-93a7-888420d3ade5","b501133f-0bae-4a51-a8e5-6b613f3be5ef","7c4458db-b662-49e5-9816-64eeb9a69063","751c2eca-3952-4fee-934a-3337b70bc6d6","8e531a81-37ff-4f52-bbec-15235168f63f","b2d96dea-04e5-4d7b-998a-84f49807846b","6fc45b8d-705c-43a2-bae1-2b5af2d6941a","0bb9a56b-8f06-4316-a972-36b3ba2d55ee","f949ee70-463a-4a8a-846f-20a44c6df1e7","98b519e3-c22b-4d6f-aaf9-f272243981c2","d4494c28-a094-40c0-9e76-f60376296b9e","c9adeeae-458d-433c-ad59-33f04a6d51a6","011a0cfe-0749-4770-b027-fa4e5ae1a916","0771a937-9711-4212-9111-845349c68357","368db349-674c-4df7-8f14-e334a5db30ac","0bb9a56b-8f06-4316-a972-36b3ba2d55ee","31a48bef-02a0-455d-b381-29a804a6ac5f","3ed27df6-8aae-4f2f-804c-3db54e93284f","efe33b3b-1b55-466c-8ea1-c8e1bc10a655","0771a937-9711-4212-9111-845349c68357","d5a6c3f2-7472-42df-bdbc-b9d3d5c42209","16596897-5cd3-4062-bd61-69e23ed06df7","8b93d432-c572-4aa1-b978-b863501a4788","0a5bd4ce-a2dd-4f80-82ac-69f301828d80"}) 
            //    ReadUsersLandfields(v);
            //ReadAllUsersLandfields();

            //MakeAllStatistics();
            //var d = new DateTime(2021, 08, 01, 0, 0, 0);
            //MakeStatistics(d.AddMonths(-1), d);
        }
    }
}
