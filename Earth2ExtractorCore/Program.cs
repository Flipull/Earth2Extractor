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
                Thread.Sleep(500);
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
                Thread.Sleep(500);
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
                                                (!u.updated.HasValue || u.updated.Value <= DateTime.Now.AddHours(-54) ) )
                                    .Take(20).ToList();
                var user = userlist[r.Next(0, userlist.Count-1)];//crashes on empty list
                user.locked = true;
                db.SaveChanges();

                var fieldsdata = E2Reader.ReadUserLandFieldsV(user.Id, 1);
                Thread.Sleep(500);
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
        



        static void MakeUserSimpleStatistics(User user, DateTime snapdate)
        {
            var db = new E2DB();
            if (db.Simpletons.Where(s => s.userid == user.Id && s.momenta == snapdate).FirstOrDefault() != null)
                return;

            var new_stats = new SimpleData();
            var tr = db.Transactions.Include("landField")
                                    .Where(t => t.ownerId == user.Id || t.previousOwnerId == user.Id)
                                    .AsEnumerable()
                                    .Where(t => t.moment <= snapdate)
                                    .OrderBy(t => t.moment)
                                    .ToList();

            var lf = new List<LandField>();
            tr.ForEach(t => {
                if (!lf.Contains(t.landField))
                    lf.Add(t.landField);
            });
            Console.WriteLine("User (" + user.Id + ", has " + lf.Count + " lfs)");

            new_stats.userid = user.Id;
            new_stats.momenta = snapdate;//new DateTime(snapdate.Ticks);


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
                        if (i == 0)//PROP WITHOUT OWNERSALES FOUND, E2 DID NOT HAVE, IGNORE
                            continue;
                        
                        new_stats.totalPropertiesResold += 1;
                        var t_minus_one = trans_set[i - 1];
                        new_stats.profitsOnSell += t.price - t_minus_one.price;
                        new_stats.returnsOnSell += (double)(t.price - t_minus_one.price) / (double)t_minus_one.price;
                        if (double.IsNaN(new_stats.returnsOnSell))
                            throw new Exception();
                    }
                }
            }
            db.Simpletons.Add(new_stats);
            db.SaveChanges();
        }
        static void MakeNewestStatistics()
        {
            var db = new E2DB();

            //SimpleData
            var oldest_data = db.Users.OrderBy(u => u.updated).First();
            if (oldest_data == null || oldest_data.updated == null)
                return;
            var snapdate = oldest_data.updated.Value;
            //var snapdate_str = snapdate.ToString("MM/dd/yyyy hh:mm:ss");
            foreach (var user in db.Users.AsEnumerable() )
            {
                ThreadPool.QueueUserWorkItem(s => MakeUserSimpleStatistics(user, snapdate));
            }
            while (ThreadPool.PendingWorkItemCount > 0)
            {
                Console.WriteLine(ThreadPool.PendingWorkItemCount);
                Thread.Sleep(5000);
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
            Thread.Sleep(500);
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
                if (user.customPhoto != null || user.Id == "039813cf-3691-4958-976a-e344bc4c35d2")
                    continue;
                Thread.Sleep(500);
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
            
            //ReadAllMarketPlaceOffers(page_range:5);

            //foreach (var v in new List<string> { "39375f76-e8fd-41d7-9c24-cbbbba2d9121", "5c9b40bb-bce9-40bc-a69d-fbdcd6cbcda7", "34a32f88-23ad-429c-af25-733872562967", "be1a13f1-ca3e-435f-89e5-1798dd080071", "b34e3f33-7593-4f57-b9f9-c337af53196c", "b8faec8c-7cd5-48dc-846d-0f2af3ba0249", "21caf6e5-79c6-494e-bd6c-a44f4533eaad", "55e9e78c-e0a6-45a2-9520-edd11c576477", "b0c3cbdf-b6cd-4f54-8fc3-72759d011004", "23c0d4f7-0d5a-4f0a-bda4-8bbb0c9bfbac", "e55f9df8-a1ad-492c-a79f-c1826175bc5f", "34893f98-5be2-4d9c-ab78-6a81fdaad352", "20efb7d5-2b3b-47fa-9792-1985e1ed3f0d", "68b38395-40ad-419a-9488-69f2132733b2", "2bbcc8f5-ee24-4c2a-8446-c594d0f44aad", "39c84b42-5447-43be-bc55-75e557196496", "7d22c1f8-ff6b-4185-8a10-f44b8d6744fe", "2c23f27a-24fc-4d85-9885-69f44f3213e7", "eca72dd0-6aca-4fd3-8b46-9464659c8fb6", "ff3cd8e1-0deb-4e7b-a5ab-fef2b27eb3f2", "5e6633e3-f305-4878-ad16-7b904772c965", "e6f4fa18-9d18-410b-9823-713bc1bf7b2c", "427cc3da-ce7c-4d81-a377-14d8604b01c7", "d55ac36a-d624-4551-a4b6-c245a53855f3", "7dde6604-5252-4137-9054-bc82a433fb08", "7cc7ae6a-3540-49e9-a069-e0455d161439", "c6bf3f71-ce41-408e-b1a1-d86625e74774", "67e39dc8-2022-45be-9b32-5e695a3981af", "7a0cc3d9-cda0-47ef-8866-c0019b251e4d", "043b660d-e026-4580-bb05-bad60f4a2481", "11ec0c4f-448c-40d7-ba76-15095d731f8e", "a3d929a1-6de1-4f5a-a46a-ca9ebc18b0df", "ec936a3c-6982-4078-8c46-55a058562fa6", "9d2154ad-59b4-48da-98e0-90f4165ffb36", "a1fde378-6b12-4067-9d51-e52ce3071279", "f6e1bb25-6447-4ff0-9ebb-486d63d7ab63", "47430c5d-0121-4513-b275-7e2f80fc6c8c", "56f7ac46-b6b6-47df-91e0-8056680b8236", "fcc186ce-1201-4b49-a59b-a5bc2d45e0e0", "9d3bf422-d655-41c1-8f5e-e8deee3d9644", "0f316d7b-9631-4137-b7bd-2b3552ce4489", "635bb76c-b22f-42a9-aa4a-64c366696e9e", "23d968d9-ba47-464b-acea-4a855c6e6b4b", "363e8672-8b1e-4c6b-b788-9263f20bb8d6", "5d28d936-4311-4846-94f5-8dbdd2045d1f", "6dfaa354-8c6d-460a-ae45-434cae0b2888", "f0d71769-0212-46a8-95bf-c242e9cfe965", "74762a3c-8d0b-43f8-b350-11a74daf63d6", "e9436e82-4c20-491e-a8dc-d45e2416e90e", "388ea970-46be-4fb6-a711-775976682f83", "3fce02c2-9e12-4ed0-b6db-76ec49aa1a0f", "e7b6558d-0efd-4e36-b2b3-efd4eea04bd9", "a5e36160-994b-4b68-b926-9f2538b6506f", "a6f373be-0dd8-4fdd-9e7d-4f35d81c42e2", "8bf9f437-3206-43ed-aaf8-2ab587b6d7fd", "297b527b-f895-4a3e-9d75-565fd681b5af", "f1895b13-beff-4929-9607-0db7fdd4f3ee", "cb08b3e6-a53b-4814-b0c8-d0abf1f838db", "36cb6733-5642-4e24-9363-205732c68fe4", "312c6f72-068c-4049-89a6-5922721a7d30", "bcafefe3-8478-494c-bec5-ddef86440689", "11590b51-6256-442a-88bb-77b539a42b86", "ba0012f1-8f72-420f-8710-f362592e7780", "cb2060de-2036-4543-ac57-0bf71ace6a0d", "0e5b4e2a-6093-49cc-a16a-c4fb92b09728", "dbf37f52-a0c7-4b43-ae52-7327dda597e9", "a696c222-4f41-4ab2-8831-72972e493804", "486928d1-b475-4d43-81fd-a4b3a9633526", "8fe33039-dfab-45d8-8c57-67fd8cdf6df0", "681a80c4-6534-4284-9cd5-0b870e2a9047", "9b7dcc2c-d42a-4794-b0e8-3b370ea71d75", "bead94a7-8d17-4fd2-adbf-5c93cacf7ee5", "8104f311-0d07-4066-a57e-34c2ba909373", "474c3e15-561a-4bbb-b755-498ecde42998", "53398049-9e3b-4d0f-b5ff-dfd0e4f903c1", "e0816e2d-8ece-46d6-b91b-032ce05a19b8", "3cf85df9-a4d7-4570-8705-2382b570c56e", "3df1ab5f-0462-408b-a8ff-fdbacc12926d", "0f0b1105-536c-40ae-a27f-0cc37eeb094f", "9bac4008-a3f2-4674-a93e-c177b329ff57", "47497593-2e17-4529-90bf-a513d01b7da5", "ab5aa398-b42b-47a3-8a09-f9709db6b368", "1e70609f-891b-477e-a8bd-dd68511eaf76", "99c99fad-4ee5-4d42-ad50-74967ac5a90d", "c5105ae7-eb08-4a09-af6c-341d840fb8df", "bc5a192c-ffed-4725-9a71-63b787f612ee", "dcbc25ab-4efb-42ac-a812-7b84cb327d34", "f53fee6a-ed1b-4b17-8d42-2c5d459515b5", "7e39c375-10ae-4e1d-9987-b20434892262", "7a2c09a2-3e15-46b8-9cb8-88bb0396960d", "df51fd18-5efb-48ee-ae06-9db4929db598", "02c3d02d-1259-4fa4-94fe-febde704679a", "2c11f6f2-59dc-498f-a82f-d47d9e24f31a", "ad4a41a6-23a0-49ee-953c-deef35b09815", "cc39e583-12a4-4b4f-a84a-eae52e9be1e0", "f418e884-72ca-4d6d-970e-0c2dde3134db", "dbb576d1-825f-43e6-9ab3-bf1598e647db", "aeca1901-0d96-4476-b29b-ba33d0c9d739", "0a9bb297-6e0f-475b-94f3-6f20ba29db57", "a6dd8ffe-320c-4bac-9006-aa5fdaaa3e2f", "a2e6fcf5-65fd-4fd2-b39e-6367aeabd351", "5a42fa18-c479-4599-8752-a880142ab820", "f41bbfbc-2283-45f1-bd83-1d844b034ae2", "d1457bfe-4830-499b-ac4f-d738557598ad", "5bceea4d-7781-4450-91b4-0d1b602400db", "45e310ea-3588-446c-8678-8f0bf5a9cf69", "d5259c20-560d-46cb-8d68-b6eaf495eda8", "f53cd650-0ed8-48b3-b909-7c3617c8ebf5", "9ef2ba29-6414-4e44-bd79-3497b5bf2956", "0960fb36-26a9-4b47-90f6-ec365cba9804", "d8890235-de3b-4b1e-b500-edebfdd8540a", "accf3833-afcb-4591-970b-441622fa2295", "008d92a0-ef95-4754-bcd9-9156b8b927c7", "a0069091-7cfb-4f42-bd4e-1edc3933040c", "1ff461d5-fc9d-4de7-a387-4b91a769ebf8", "4ced218a-a1dd-46b4-afba-3f02f5429816", "1964ed37-39c7-4aaa-ac76-cd716a3a060d", "ba94a17f-e348-4c87-906c-186c5c1398bd", "6983e945-a316-4c02-8b69-a13eb1e7b83d", "8b14b76b-185d-41f0-82a0-c1b8cb3ee8d6", "db3d6038-e851-4960-871b-74f5095e481b", "2434219d-9771-4190-8704-6c0675f57ce7", "8bf7b17e-91f0-4e7b-bafc-3e7072e7e788", "94e2190b-c1b8-43f7-8a7b-ca87f4bdf0d7", "7442a96a-5af3-47c6-ace0-869358cd051c", "eee0d8bb-100e-4cc3-8f35-c9c25c18f505", "a47c3de8-6d44-4f88-bef9-d207ed6167a9", "c3819634-5a03-4118-94f5-b32f06332b6c", "a568cd41-e33c-4d36-91e5-729bcf8abef8", "3e91764b-ac31-4a3a-b6b3-6bdb6cda00e0", "3502a922-36ad-41b6-96b4-e769f54527f4", "87abfda6-068c-495a-ac9c-7c4042cd3574", "9a90034d-900c-451e-9982-0ca013252899", "4172b474-7aec-4ffd-a278-49b98a08051f", "b3df46b0-45cf-4af1-918b-94c24af87d71", "74a2ccd5-ca7f-4a9d-a177-baf00e609d23", "d91bd915-c994-4b0d-b16a-2d22dd2eaa14", "3ce9997c-42ab-4d7b-bd33-e9851ca9b9d1", "444aea61-edb7-4f3a-ae2c-113e42ab5ea8", "0ae7b70b-7c05-4ed6-b2fc-0fda487386f7", "a4b376b6-7ff7-4c87-b680-c2f3e90c4dc3", "cdf0cef8-db49-4f02-8688-ab7ddedaa534", "710b9d6f-f8d4-4881-a731-a230dbb23c4c", "e9080098-7157-4485-9970-edefd1bcc551", "92ebf849-d2e6-4732-b557-c77669746aa5", "47fc8f2c-5086-4b47-816c-1d7a745afea7", "de21b7de-916a-49a3-ab7d-53489bdc48bb", "ad7ccb5f-0966-4c54-bd9c-73d979b32dd8", "230dd9e9-a4a2-45af-839c-5c46625da6df", "294bce0a-a596-4812-875f-5e34e8a5833a", "a599b710-aeba-4e65-a7cd-890948b1a644", "381f12f2-b973-436f-8376-b0084218a8de", "57c6de38-4654-4345-84c7-9b048196f9d7", "d4e3a83f-9373-4839-9ce5-705d4a33b94a", "8aa43164-22b5-4270-ba5d-dcb5b4a047f0", "93e70af5-1938-4ed6-8a85-fed95c800d0c", "62121c40-cc96-40a0-b93a-beeace0fb11a", "f3f9fd40-6218-47d9-be1b-ceef2a121548", "00cd3965-d554-434f-bd92-3632615e3f4a", "730014e9-5d67-4914-b39a-f611d41a0f6c", "6e75bb6e-7bd2-4f0b-8d83-fbecbca3bc88", "6e21794a-48f8-4648-b5e8-e647b831f8f1", "56bf3532-dbaa-4d84-b265-0d4094a792a7", "8e0ada8c-0776-47e5-a27c-6d905e3fedb2", "a1a9b5c8-443b-4346-87d8-efcd1f769211", "7892713a-6331-44f1-bc0f-24edc16a7c16", "9493bf66-4645-4db0-ac84-c22eed0904e1", "3826cf48-cc6c-46e1-840d-f800886f7a4b", "9897e41a-fcb0-408c-82df-d0e38ea071ce", "77637d23-405d-4b11-bb36-7d2f7cb738c4", "f1f9493a-185d-4826-b383-6cafcdf91d9c", "ebb25c34-7a1d-4ea2-861a-6460551e2a06", "a98d7cac-5b37-4a8a-92b9-f375d44720b2", "08eac5c8-5c73-4c8e-b591-3e6da7a9aab1", "9d0528d7-c191-4e28-ad2b-378c7f3a6e72", "5e9717c5-7459-4fcc-b3b9-ef07bddf6836", "65fb3bc2-03ac-4978-afaa-1e1e2ec190fb", "c66af8c3-ad83-47b8-9e5b-dd58230d45aa", "495af3ba-f821-4516-ace9-4d532c80863c", "8d5daba2-6c50-42d5-8331-a03783fe14e4", "90316779-3e74-4061-8ebf-fc1b5c81415e", "87aef4e8-24da-46ce-85f2-1cab2e6b2607", "3eeec2e8-402e-479a-aa9d-0c65fe8e9496", "857bf6d3-428f-4402-98eb-09cd76baad51", "37e24d71-e318-452a-8780-e099cb36c20f", "490d3fae-3bec-4d8b-8929-6d7cfbe02407", "edafb0cd-dd82-4c9d-ae61-e5e76153a660", "b8c8f9f5-1c62-4074-9846-a90161e3acb5", "3a8b63ae-d9a2-4ecb-950a-6f04dc7f16be", "90b67d66-c960-4341-b9ea-0e2a7be849da", "974cec85-c628-43a3-a7b7-46d197b92501", "761f8290-1f87-4743-bb52-ef1161484db8", "f31d131e-e914-447f-844a-9b33f73a66e9", "a7d0f997-4c45-40c3-ad89-0b4168486b98", "878d6627-9f37-4284-b09f-22efd35aaf0e", "dc98c4cc-1c55-4d68-99d5-041df0f60431", "3aeac968-3c03-4da1-bbb4-bffd521bb11b", "2bef197e-4b8c-483e-8020-d65d24772b4c", "f62f65c5-8eca-45b5-8469-214a6af64005", "7f2b6278-af72-48bd-a7b0-7b11e0af2e17", "5add3f7f-3e0c-47a5-9677-62600c2a9813", "7b872a34-1830-4f71-a745-80f4b59a6cf1", "451fef77-734e-4edc-b2d9-19caf30336eb", "77dac35c-300d-4ad8-a05d-b97c8e372e84", "eb9a876c-df4c-4880-8db5-9edd4735eac3", "04a6e9d4-51cd-4115-a437-7beec01a74fe", "c84e5b00-1e01-468a-ae16-05828c63b9ab", "b37da678-90c8-404d-8400-10daa0ee7ce3", "4cfd60ef-d59e-4e4b-9016-2b33a833c621", "30900c28-aedc-404b-a2c9-8cd9dd2a3bc6", "c6523fcc-8531-4f11-8b84-4191900bb0d1", "35bcdaa4-2951-4f4a-b5c4-67041fd66c35", "a478517c-4fee-4dda-9a45-c1fb343ffe36", "8f55bb69-9ded-4784-a49f-2be2bf143aee", "48417f01-cbf3-469f-9b95-675dcac5bfc4", "1e2e7493-334e-4c65-89b5-e482c7df4b67", "fce41933-9722-442b-9fc6-d49046a0c88f", "e1adfce9-b0a7-4a58-b3dc-f9c8fa6472b1", "66173212-87f0-43c6-a81a-5ced133dc73c", "00a21d34-3e11-4ba6-97e9-93c80d7834f5", "525a6c6b-0986-420d-b7a4-b1786959a04e", "6c08c96d-f5a2-452f-ad2c-f46a6fd01581", "9cf6c62c-dc04-4c64-92cd-ad2c5d504fb1", "a85cbc44-9ad1-4b14-9717-79283bbae427", "ecb4a8f4-2744-4598-926e-7007a7da0657", "f1484a86-7181-48e6-a3b5-e593efb72ce1", "4d644853-45de-4ef2-a1f6-f349b27a5ad4", "3d0fd9e6-559f-4291-996b-b898c03d32ca", "ba0c5e87-1ddc-41d6-bdca-cc620a691aac", "cfb9a305-a14d-49b0-a87f-f76c69b85d42", "0c486cc1-e80f-418d-8167-e5b48d9633a6", "b8ff3633-70b5-420f-9cd7-b9b3cd890a5c", "a5c63761-e5c3-4482-8909-0ba1a33eddc3", "89a993a1-e34a-40bf-8e0a-0159826bd15b", "3d723e48-6e60-4d39-9dd5-77c86b2d8f43", "87a663f9-031f-41b6-8f36-5279b85a60e4", "b90b82dd-b64c-4608-bd25-32583c878b4b", "828b282e-1284-48ba-8fe1-2ee0189b4a58", "dcf9ae0b-f84c-4294-8068-9eea7d171f96", "ceecf0df-cee9-435e-90da-9008ee9504ed", "257476cf-ff21-4484-9ea0-935eda009043", "3e58dec7-5d2f-4292-bac7-687adf1eb625", "dd7f482e-e180-4846-9570-0f54fd25ffbd", "f8810d3a-afd8-4897-a5d5-4f526ebcd639", "3e32780d-8425-4e0f-b1d8-5077872a59e8", "5906566f-b6e2-42b1-bc34-ff657324e014", "961429b1-2c3c-4d61-8a29-1d7c53fa94d8", "fb88683c-349c-45fc-973a-d10af444d938", "574d8cfa-ef00-44f0-9a21-cff704bfeb1a", "f3579877-2292-4310-8ddf-a7f02b8d330a", "f55a3fad-3ccb-4758-9595-0d6b6168e9fe", "c42f8b3c-4706-4f0d-8164-5e786f08da90", "cdcb11bb-0e3d-45f5-9860-d43c2cd8cf8a", "52ae3c7f-f2e4-4b84-a69c-4725b529add5", "f046828b-fa3a-46a9-b19f-59078bdc2325", "64594aee-2a89-4cc4-966a-b21682bc0a75", "d49a80a5-a0d2-4479-a729-d17ab4db4f56", "b6f22d8c-67e3-45e8-90aa-bc5501c791a9", "1d1c171e-1969-47c4-8881-b76ff617e484", "4226329f-9fa7-4597-844b-a7f01e5bf66e", "0cce9cb4-1678-4d42-8dff-5d412c019217", "075f75ea-1e45-4276-9404-f6bc69bf145b", "e907165d-5b55-48b3-83fe-d4f215fd279e", "06ce98b4-6941-4654-9cd7-9f33c7ebf44d", "9931b301-7a6a-445e-80dd-ef864dc58af3", "d407ec32-13d9-4e75-bef1-83ddb1081458", "72222c62-f056-43e5-8a46-6c5618ac1a19", "6ce20e9a-26b0-4243-bd89-86c44a69c451", "044051af-568d-41d1-bded-6300c142fda5", "5c9b6c9f-7a19-49e5-bc00-67f15f036ddb", "aec65316-7a9c-4e7c-af60-a44b37ef3eb8", "a34b945d-98d8-42bd-b161-0823f2955dc5", "f22a77b3-2be7-48b0-abf4-5d3aa93f2bd1", "a87718ee-3be1-4239-a998-5c5ad07ff6d4", "6101e9e4-f2be-4e9d-b3e5-67b847faccd5", "6f7be817-8201-45aa-ad9f-da98c467a17c", "9902d5a9-b051-4adc-ad41-f92467e50c8f", "61c7bee6-7c28-403c-b04a-1b579ea9b156", "3904fafc-8b7f-484a-a6bb-6bf7c86db62a", "e9cf11d7-9090-41f9-b8a6-a47d939bdb6a", "84074bed-a1b6-4045-924a-c75dfffb892f", "6fc45b8d-705c-43a2-bae1-2b5af2d6941a", "edec97a5-1c27-4d30-a64b-7ccc0d45cb0a", "95b38c3e-6e01-48a8-a889-9889a87d511b", "b800091e-8c41-435b-a141-d15b34b1040f", "9ef6eecb-adee-46f5-a81f-591f4c931457", "3cd7edd3-3565-429a-9121-fa1d33812572", "d08b55c1-6c04-4f07-b0e1-1679301bc940", "5c7a8986-f821-4763-87b2-a114e1235a52", "316a8bc6-027c-40a4-b25b-ebfcb985abe8", "68e38fc1-f7bd-469e-a8d8-9ca17cb365ef", "0128a24b-bcb9-4c34-a6b6-e0cb9dd3ad26", "2edfbea0-5625-414c-8477-0f6c0790ba1b", "a0279c2b-6cf9-4c35-bd28-b21999eaa4d3", "eea19e25-e5d2-436a-b688-1e9d1536da5d", "65386b7b-a517-475d-9796-9453c01d4e8a", "f944124d-f30c-43b3-ac49-f8413f965b8a", "9ae071af-6f98-45da-9483-21d64ab6e828", "b73eece2-94a1-4d4b-a893-b6aee9cec6b3", "21ed0048-327e-41d4-ad1b-637361c2edf8", "325207f0-402e-45c4-ab96-85553ec57016", "6b77509a-5009-4e12-aff4-88132339afcf", "c42dd427-c265-4725-8aec-da3c6e0f655d", "6f8bd3a7-bc8c-4a0a-9e88-55a8fc558661", "445194e6-2a47-4e97-a85a-70df440c3521", "a30b5de7-e533-4fe5-83d5-2758da863293", "8e03ddf1-2452-493d-ad75-94702a68274e", "83d0fcf9-d6fd-4058-a338-1309da06778d", "d8318218-58bc-4ecc-9bfe-a0bcb0510f3f", "8964fff8-1d74-4411-983d-51375d4778a0", "a61aee1b-ef3b-406b-8210-f9a9a0b56f16", "68cc8b40-b9a9-49e5-8e89-e29b09cbe3ad", "db7d98a3-369a-41a0-803f-622ccab5d1c5", "8bdfa0c1-ba91-4272-a406-07b4789f3ad9", "fcfc0b5f-45f3-42d0-a258-479794fcaf22", "f644a2fd-1b60-4398-a449-cca93b5c6741", "71400e9e-035d-4295-a3b2-49c927d180d8", "dc9c0273-af63-4ffc-8966-2b117a1a7cb0", "9283071b-d026-4ec3-bfc2-32674239758c", "208f3900-2b76-4569-823a-4ff9f362060a", "f51efd42-ce1c-4a3b-9568-4d0e740fd3ad", "2f48f432-e1bd-4e58-b158-21b10a54d280", "7042ea1b-f8d7-4695-a8e5-5e514eb7d066", "182d331e-2c7f-47e4-a6ea-babfe2f1cac2", "c3e6963c-e8f9-4ba3-a370-f5d2abbc05a3", "07ea7433-bb4e-476c-8fe4-cdf3098f1676", "9e9add6a-df4f-4be6-88d9-ab602749a817", "851f8105-e3ce-43b0-be10-208cf8d143ac", "7341855b-897d-4a1a-883d-81e3f87516f7", "32007b0c-0fb0-4c9f-b857-6dff94c43c49", "cae59d5b-e2f4-4309-baf7-9b6523418766", "43fb13c4-82c9-4228-a417-145fb350a0f5", "7a496403-b9de-409d-a4ea-9ec54868e7e3", "63329501-ea07-416f-b1f7-e5ba80f15091", "75b71fc7-27e5-4c4e-bb9d-b4e082db1f46", "5e6436a4-2ffb-42d6-a629-072cc7c73968", "77eba370-2480-4edb-8b04-0986046fafa2", "aab5c10e-d0ba-4321-a025-f80db1d59601", "9da66bbb-e5e2-482f-a065-6c437b5bca52", "4c7e858c-709d-4d4e-9613-c81bde458b3d", "571b399d-fd33-4684-8094-1e468498fd71", "0f202cc8-739d-4d1e-987e-1714ae87e0d1", "6a25304c-07e9-4455-a084-6e134add9206", "2af56ff5-2c67-485b-b998-0801b61f13f9", "a99819b2-ffc6-43b6-aed7-a03e9c189e87", "1efb02a6-7535-4726-93f6-fe748df5b00a", "2931d5a5-c38f-437a-9e6d-aeaf362d2517", "62965de4-67fa-4497-afe9-1e432d77e9b1", "75c0619a-c9ae-4dfc-8a25-ec228204f268", "95a69139-7358-4b12-9cdb-5c842063c567", "974560e6-832a-4778-ac1f-cb16545335ea", "684e1459-0bec-4b22-b490-eafc707760c2", "789c723c-ba7e-4b70-870a-4752cda45025", "91b9d13f-3b2a-47ea-b323-9e1b22eaafd7", "a40daa20-3273-4cb2-bcb9-a241106a1c77", "7d111cc3-efff-42fd-bfa7-6dc2cf855ede", "98090b07-2b33-4f31-a28a-88fb0af91cb0", "6157918c-3b92-4df1-9a47-e25d75a8e05b", "cb6ce533-58fa-4962-8d82-ad31902ae6f5", "2da3c714-1337-4d92-8f44-73408be0549e", "c5d5c0e2-86f6-46af-8143-9cf18f57975c", "2d8b71f4-3b5d-4b28-afb6-43a4058b052f", "cea8ea76-c6ba-478b-8a47-602ac6f338be", "b19a3fc4-d491-4e5b-acc7-0bc2370f5360", "0fa9a508-78b3-424a-9777-1d6147c8067c", "46e243dd-6cec-49ae-b43c-be9130086b40", "72e25ea6-e0c3-46fe-a997-be1a32835a06", "ecfc69ae-d020-460f-8d1e-e38cc9f1bed0", "cb3d8d05-acac-4ea9-b2e1-837c2a2b846f", "84016977-7628-4880-871d-97d080cf8d40", "2a361a47-cd63-4e55-be23-1afad9cfc9f1", "16e6f585-e5d5-4563-8978-ead1e82943d2", "881c7dcb-971a-4ce4-a7c9-824dae4cb67b", "0dd1f9d2-abc3-4ca6-8015-8aec53c46046", "5f8ca598-52e4-4020-8e6d-8a9d2e6f6532", "ffe27984-5899-4448-b793-f82cde7891c9", "ee071071-a584-4bcb-b70a-fd06e764d12a", "d3420ba3-91d7-457d-8c64-ce2e2c102f1d", "f3462b31-3b80-41da-94ca-80d18c4a4a3d", "62e0d05d-026a-49d5-b9dc-ad5c7e72a437", "b73f2ee5-3c0b-491a-aa3a-052c773e7851", "26ae6827-f855-4de3-9a28-6a2494ff7602", "dc2a29b8-e579-4d53-906c-eec98b25d69a", "1c4bbb3c-1823-4f45-9f8a-8d892c45b3e9", "91c924a7-04be-4c85-a774-85c92c2b3b5d", "176b4f71-0b82-4b2d-9d85-494f15919c1d", "60537046-636d-450c-a6db-2fe027c5836f", "9546c786-4785-4661-8fe6-f085fc5f2fec", "c8923286-f3bd-463c-8a8b-6ef31f8ecc9e", "4ce74885-e6c6-46d1-ac37-7c5a14f01e9f", "6d5176da-2321-4820-87da-b2a1fcab8a04", "bcd73701-3ca5-4913-a768-cf7d8f06ea5a", "5336ef2e-22c1-48f2-87fe-8ec76e0c6037", "9854585a-f9d9-4e51-aaa2-d6bd09f12f87", "809a5a75-4147-432d-a292-5b204494753e", "6679f53f-9c17-4186-a2ae-757a2b5d3229", "ca3d1658-c83f-481f-8bff-9846ddbcec1d", "f3957965-e9b9-4696-ab77-6ddcc1be0e24", "3f7f0e66-1ed5-4b5f-a10d-52e2a426c453", "36f0e049-cc1f-4410-ad10-5ec2573c5b3c", "2e138a37-59b1-4726-b000-1eed4788ee79", "d6f143bc-76b1-4a9b-974b-48429d277822", "40af28f2-5a12-4e94-9866-2d26509da7e6", "79093172-d02c-4646-9c34-81d065ef5366", "7adb7fce-c449-440d-afaa-757307df1a51", "4cd91920-98d0-4eb6-a761-20e760709102", "919e5884-a8d1-4ffb-9879-c0fa900c0b4b", "2c7a132c-da83-4948-abc4-cf4dd4a2619e", "d7ebea3f-a237-41df-b0f8-2039d51ce3ad", "e6a601e7-096e-4aae-9045-58b04c1c49db", "055caa4e-bb53-485c-9c2a-7dc2b4755f2c", "a59186a4-bfca-476b-82a6-404d23a12277", "3351d263-b191-49a6-8e71-56492b0b91ba", "c889a42f-c57f-4114-8b81-9ca94930fe42", "5f4d0d4c-d58e-451b-9048-20ba8204a111", "32121b76-e160-447e-b6cd-6cf5a2b75c86", "11149e51-4240-44ad-a70b-20f063628d44", "7b167474-45ec-4d4c-9a4e-8b168e6efd76", "dfe5c4ab-9e61-4784-9e8e-966f12a5b6ef", "771762d1-1eb3-4989-b9e4-d990fde19533", "5b26d46a-0c98-496c-8e9c-27dea39996bd", "b014aa1d-18a5-4c43-be68-41bb74a4323c", "072deeb5-5031-4adc-baaa-ee013ce19183", "e35d1133-bb81-41c9-b66b-eed6f805772b", "4f703e0f-f940-405c-bb58-93cd589d8e7d", "c468981f-6866-4795-ab28-45b453791523", "3d57be3c-5135-401b-bc36-57f899ab7d6d", "185b8058-0020-49be-82ab-e846c57d7ac1", "c087ee0f-6db4-4967-b34b-5dd481914396", "b6e83d64-d586-4667-82aa-fedefbc0569e", "735d7623-bf19-44e2-9d7f-d777efe8ac9b", "86db9b73-1308-4ee1-a072-ea82236eb42a", "c6c888f9-ba91-4700-9abf-8389ec2c10be", "4af7f3b5-501a-451b-83a5-ce291152df4b", "b9436477-8ecd-45c3-a4a0-83baf13e9d4b", "c311b3eb-4cee-4ab1-916a-65a6f3e4ef52", "380c5802-6fbf-4220-a795-3b537a6070a2", "fc413eeb-3ec1-4175-b995-62de545026cf", "52b26c97-83f1-4898-bbfc-dc8f2f62bcfd", "1e55d37a-1b0e-4490-9e0d-8cd676afd320", "5a49db5a-12b3-4bf4-8039-2669f18ebf38", "e57cb4a4-e785-4085-abdf-b77e60f518f6", "a38ce0d1-3a21-4d43-9499-d17c14da668a", "654a908e-d01e-424d-b80f-b17d5090a20c", "c1052acf-c8c8-4d7b-bbf5-cd7131c9cbcb", "1f443f2b-f4de-4d66-8f0a-202fe321a3f9", "b9d86621-4d2e-4294-82c1-ae6a31c98e76", "0557c23b-ef7d-42f2-a64a-4f255070f940", "de7db18b-a594-445e-88e1-0f0e2d74e7ef", "ce96bfcf-a338-408e-8a10-6c6f6afb01d3", "b0a1deda-5c5d-438d-892f-134d07919747", "d8200232-a9ed-4682-987b-4bb861c2a23a", "248bcedd-f456-4644-ae2c-33ff80e051d3", "4a18e998-bb4f-4ceb-8b42-7f39874bc574", "dd773a4e-41c5-4fa4-b7a4-2040b1343edb", "7ab56ac6-a365-4970-bb81-6615225ffd5c", "af4e91a7-78e7-4cdb-97f2-1136afa33dd7", "68f77884-f2e2-4a1c-9751-2acdf15360d7", "3ac888e9-293f-4795-9f62-270065bd6291", "4a47e7fa-0839-4ebd-a87d-b34f9ee7b9fe", "587d5c81-2431-4b3a-b9b8-f996ef1a144b", "6889d72b-0c00-4437-9594-8c74676320d1", "e80c0928-af8b-4792-9275-88410c70bf12", "584f8cee-2d7b-4418-b8e9-c00010131ec4", "e13896a9-6655-46c4-b8b5-27450122a379", "d0adb629-ea7e-433d-954a-31494291ef8b", "c9adeeae-458d-433c-ad59-33f04a6d51a6", "7c4458db-b662-49e5-9816-64eeb9a69063", "5d54662c-a071-43df-ad20-4bd150a4cb9b", "36b83994-4fdf-44e6-87b6-46db64bdbc8f", "c8fe0fa1-6cda-4745-b8d7-bbcb35e13bbe", "e787af7f-683e-4532-b8cc-2935475b2928", "02626011-cae4-40ba-9291-31add8037ee9", "10e0e29d-7d2f-4516-8d51-581ad9ccedb5", "dc4ebe2c-3c5b-4fe5-83a3-d9d3ead22195", "1c22f1a3-104e-46f4-b8c7-f982957d4f29", "ef8acbd0-1614-43ae-bf43-751c1de69463", "0771a937-9711-4212-9111-845349c68357", "98629496-ad4e-4492-96ec-2b4b4962395c", "5d0fa626-c4d1-4a42-98d7-3f105546dae5", "4033bdae-f8be-4a9d-9f64-f7a2e2549cc5", "17b9f79a-6244-431e-86fb-0eabb273bb50", "42aea103-989e-41f2-8203-f4c6bd3495c2", "06a33dc3-0f28-4a5a-a1a4-0db050153a60", "c2aaf290-7655-42d5-aa6b-490698ee0260", "64ec7073-eec9-411f-b7bd-cd269c8ed5a2", "96c00391-ab64-4035-bd23-6943eacd6a9b", "856f8f20-94e6-45ed-85bf-a28fd019983f", "043320cf-4543-4ddb-b495-539465aeb21a", "347e453e-23af-4873-9881-086e379ac0fc", "cb14c5ae-b764-41b9-846d-bddaec4a406b", "3d88a05b-65b0-45a7-94b8-35268ca1f389", "1f1c4ed4-954f-49d3-b22d-d8a39bdf12b0", "ac9c9dd6-f3e2-4e46-8a39-6133b30c8f1e", "33e92193-878e-4fbb-852c-a4a797b05ce4", "0b4a116d-62d0-45ec-afbc-a62157070959", "90f5438e-c67d-47e7-8e9c-15a83352504a", "3e6a4823-b0ce-4059-8fdd-10b5b98d3962", "f2b70763-0446-4451-992f-c85275e9a8a5", "ef6b0fee-3786-4373-83a0-94f3a4171b2b", "d0a57445-da18-4279-8083-c01c0ee38112", "61832145-1b60-409a-bef6-6dab049f586a", "10a1f69c-1628-4add-8f1b-dd8a280d643e", "277a21dd-9673-435b-8b33-f8fc4faeb937", "5b3a2fae-d594-4ef9-bc20-89d1e4b90ac0", "7dc8e9ab-bf9d-41e1-b1c6-091c62f47c1b", "9691ad0f-dc9f-4ec1-9598-f3e313ffedad", "2a6b6146-ea4a-4f00-9494-67fcc4e313c5", "ffc9b303-5eb4-44b8-98a0-846084f993ee", "cd80b1b3-e17c-4f0a-a123-19c1267bed9e", "eee4ca23-f3dd-411f-ad14-571d9398bc16", "d5d68948-dd5b-47fd-bfa2-c0cb774131fd", "fa544c10-708e-406a-a5d7-fa8bba98dbac", "76196547-d771-4c21-ae85-a97c1482758e", "52da850a-862b-4114-a0ae-280f525ea428", "3e8d93fb-7c89-475b-8d71-a9bf825186e8", "5b784a0c-b155-4a66-8fce-9f62d8470cc6", "6342d7fb-6dbb-4651-9b1f-5e81eabdf28e", "d2bd0a78-fa07-4fdb-a70f-efdcc5323805", "f7aded5a-05a7-491d-94a6-1756d454d3ba", "dddbd7dc-77a9-46cb-97c6-d9934ce7c856", "5b3fbff0-25ac-47c0-8046-d710ba5d2cba", "06ee5716-343b-4977-947c-379461c8f877", "1e73ca9a-06a4-4c21-87b6-9df663e9b288", "eb7b820a-5b4c-4428-8271-326276956a81", "7fc47ba2-d01e-4c34-80af-c15ff322d67f", "dfe7b5ff-5834-4761-a7ef-a1345f03e869", "5b6ce8a9-d337-4962-b4ca-34323b0a7577", "5ae72b56-1258-4d0c-a45a-598f317d0f51", "b85dec4a-3ad2-488f-a650-269bfa52d10c", "e09ee053-3379-4e9d-aaab-709ef2f615c2", "99e45e66-57ee-4ad3-a40d-1f02974898f1", "74be45e6-c60e-4251-a024-7802d9764267", "5e791413-a51d-402e-8a1b-71147b1a603a", "a1245e00-7483-43f5-b36a-457a01262fce", "6ca84102-a4d3-41c7-af32-cab400164a47", "822a3e1a-f95a-440a-80fa-034d05ef5973", "a3e7028c-2eac-4028-a19b-0d29c77ceef7", "5dd9359f-6ce0-4a92-a8c7-21ef67a4070a", "cb64fe85-d0ae-4372-8aaf-7a5f80ad448a", "71847c37-69f2-4f01-9b8d-1895d4699b98", "df5dadc9-4fd3-42cf-a365-be3f6009a5bc", "e23b7fe9-30ca-4b66-94dc-36a360503bc9", "2b726471-9abf-46e0-b6da-a50735833c65", "3e39b700-9a66-436f-9e87-697a4844845b", "64433c04-98d1-4fdd-926c-c1219a86f048", "b9d3bbb9-2b99-4ef0-b674-05f8e01ba375", "66c758d6-81de-4a95-b6c5-ca75fad447d8", "f92944b0-0525-4a27-a4d1-95bbbcfac288", "9badafe3-3b19-4154-9042-c4144ac90f2c", "4252fbb2-3b21-4138-9051-79e3a043ccd1", "b679a09b-58a8-4fb7-ba5d-c09d81c51ee0", "b84c0e31-83ee-467c-879f-17d8ec66ea44", "8ee53f18-2008-4603-8d3a-89186d5255fe", "5a205f91-ee8c-4efb-929e-960f88dba7ce", "7302a062-ca6f-45f5-b0cd-5bfe259ecfbd", "15984324-a6cd-419e-8eaa-26277fa9184a", "8ccc67a9-721c-4001-976e-b4e3b0375fba", "47fd89d8-4ee1-40e2-a92c-65434e51617d", "d5a50d28-a854-4d20-9e40-762cf40133d4", "2f182f2e-4b3f-4e2d-baac-dd720e714eac", "b2b7dbe0-78be-4db7-95f5-ff07caf5ea7a", "88cbcb50-32d4-454d-9498-80bc4f05e261", "2228c6f9-c4ed-4761-890f-18f65850dc55", "63c3b8f2-e6ff-4c3e-8e73-f528510f3bb1", "520c814c-dde1-45a9-ade5-f40f2a3f7913", "7cdb9739-831e-4161-bc6b-aafa740e190d", "35c97321-10a5-494a-951b-e33b02e02654", "ef8e6871-91ac-488f-8870-5e17bba71f9d", "1d4aefed-6129-44fb-8918-0763954c2feb", "2b8fba94-4477-49ef-81e3-bc4717564c27", "ace69afd-12b9-45ed-a8ae-d4f574132e89", "2f620fa0-c5eb-4b99-8920-72d44a0a5852", "4019c86c-5ce8-4e00-838b-1429b84b3190", "51f632e1-d19a-4974-a9cc-d57c21c10300", "47288980-5b76-4a0a-915d-f787635b1561", "b3587e9d-0f08-42e5-8d65-92b29c6781cf", "2aefd7ee-e838-49cb-882c-4fb767d7ddc8", "d637689a-cf15-43e4-80a8-c827e23a7d45", "e3ada3d5-ca63-4357-b886-4abc9fc19f46", "8777645f-bdf7-40b1-9610-e6af2c2a055d", "ece4b52e-671e-4b0c-b543-3b1cba8a94b4", "f13ef3a5-3e11-4bad-912b-634577a8d26f", "9f05ede7-da78-4737-b637-00662c72cca9", "d0e6cf8a-0a56-47f6-865b-8e2aac77d1a2", "fad42932-ddaf-40ed-9702-5a6400d58607", "8738b17f-9cdb-488d-9b8b-3cdec11e14dc", "51e86a28-88df-4bec-84a4-a33b3e476790", "5f8dc4a0-5616-4d64-81c6-b004a4a1349d", "e4a065dc-b8bc-488a-bec6-16e2788111e0", "460bb9c3-eb0d-48e6-b40d-3518d57450b7", "032020dc-b8fe-48fa-b592-9016f380fe6e", "31289889-ca56-415a-87b0-c3457a382833", "67e7398c-fdc8-4281-b774-8d2759358859", "384e8fc6-7ed3-479c-b106-92ddd41b60d3", "2f1576ce-dd13-4bd5-8faf-9b2aeaf54c7a", "27ece289-cf60-4f5a-b568-bc5699813cc7", "a64f4a23-4a02-488a-9e7f-aa8aafe27cd2", "6c190c05-1e17-42ad-8ddd-d539df7c31e4", "511601ef-1033-40d5-bb3d-4087d5eb6fe1", "93bb769f-6852-4b14-9009-6c784b907cc0", "3125a70b-a07f-419e-8f02-c3dabb47000e", "7d992afc-6147-4c2b-8b6e-0e7266240417", "5c9ad38b-a993-4a09-b3a7-953fea68abd2", "661e723b-5eef-4555-93bc-bd55955809cb", "3fb91fc4-6c37-4d23-bc26-96288589e4f8", "39dea3ee-089f-4787-a27a-bbec3534a9a2", "18ba8f21-9234-4169-afa9-7f9356d70ca8", "4a51b278-ef14-4a37-9005-2b321bf6dee4", "68eb54f6-8b1f-4fc0-a881-aaeb945aca66", "a4798d11-2491-4e86-b582-f8154c777b36", "8beb6335-04ec-4a34-847f-762b102c55b6", "f6fd869b-42db-4254-a947-4437bd3b8610", "7192300c-1090-45ef-a56e-4c21a8f97c1b", "6dcafa2e-fd80-44ef-aa57-881bb9b4b63c", "28de5536-5059-43b4-88f4-a4a81efcdc26", "f7870250-6917-4803-a06a-83b77afa83fe", "950a3ca3-c454-4697-a6ca-0a7652d2685a", "a18921b4-576b-480f-9129-3ad1b1cba51c", "64e7fb9e-5744-432c-8633-93bf31ee2f15", "0fae6579-5360-4e0d-bb13-010de2480d75", "bdc95143-e51c-450b-b8c7-6dc28fb429ff", "8b287459-59bb-424e-a15b-c77bf5168eb2", "370f3ebf-60fe-4182-a31d-c2ad07f22249", "69fc25e3-4dda-4f41-bfd5-a283d5db7bc5", "015ca78f-21a0-4806-830b-d0f8b7dc1097", "bcc3c378-cd23-4a5a-aa5d-7261180a8df9", "8e2ae8ff-3af8-46a2-b9c2-d688c6dbfdba", "8e531a81-37ff-4f52-bbec-15235168f63f", "323d5b84-e1c5-4d68-be75-d796bcef650c", "a9105c38-7f58-43f7-aaa4-d063c2429197", "fa7084c0-2f5d-462e-a630-f7eb3b0264e4", "407a028a-681e-43bf-9d9b-056cb768e619", "657287d1-7359-4cc4-ad13-b0fd51e93fe1", "054b44fb-9206-468b-91f2-a1351e9bdbcf", "7051096b-f5fc-45bb-b73f-b638acabddb5", "36546971-4c26-41d2-8c26-15c5448ab0e5", "3cef7b1b-be03-4f06-9195-d43062496d13", "e095d613-e2cd-450d-b092-a34872c2a507", "873c62e3-5d72-4580-bffc-9bc7e9a6e644", "4468d011-9757-4dbe-b01e-b0fa82143076", "700b2937-8564-46c8-a944-f1de345850e2", "20b70eee-6120-4ec5-b428-927339b810ff", "f10a8ab4-38d3-4e9b-96c5-66224d4f7f69", "b0a8c879-106a-4f72-af7d-809d972d54cf", "b860b949-5ff1-4641-8178-f614d76632a3", "4beb0082-099a-4aab-9074-89de5a99903e", "6f7d350d-c2e8-4014-a1ec-10ed2e66f69c", "3c86efc7-653a-432e-b9bd-e6735b30f257", "ad71a879-f057-4575-8c70-ac31b2d1c417", "255816c9-bdb0-471a-8467-aa43ef5e640e", "217b23e9-b10f-403d-ab3d-114da589cc2c", "cc997554-0645-403c-8109-031d75600063", "589786c4-fb3b-4734-8f02-db58818bfef1", "abb91498-e265-4fde-86c0-e0fb9ed41af8", "68da0c12-7401-48b8-8280-814f907601db", "db9a14fb-2132-4a14-b7e5-6650ec912195", "801e315b-79ec-480d-9816-52b6e2cad4c7", "496f3f66-adef-4bae-8455-61ac6d34e588", "0d6461f4-a9a1-4e49-a90b-d11f7ff80036", "adfcc5a4-2f5d-4a7c-8b3e-597d65fda113", "609488f3-ca5a-4a96-b96d-fcfe5f9234f8", "6198f436-6b4d-4355-837c-187792b3249c", "8588acdf-7064-4bd3-a44f-a9179b1fe30b", "80781b96-1760-4c12-9971-d600eb554681", "1d1c6c71-8f9a-413c-84aa-177eaaa84d74", "2910dc93-4783-4561-b219-01b1cb18928e", "daeddcc0-dfbc-400b-8690-ec08caa4b984", "eed97321-5dee-4763-a925-9d52b3eaf3de", "20120472-f418-464c-886c-0f0686b4aadc", "566e2024-b960-4ebf-a941-d82d007cd2e1", "20d31883-6028-4a0d-9eda-f931eed8c8ca", "be0dbdd3-1355-4ff6-99ee-539c822018b2", "a229893a-7b15-4b1b-ad84-ed734296036b", "f4ef1c4d-42e8-44f6-8b7c-ce28527a07e6", "153087de-b79c-4974-a7dd-378c37053b8b", "ff7029d9-815f-4c3f-92e7-2cf54a66eb9b" })
            //    ReadUserDetails(v, skip:true);
            //ReadAllUserDetails();

            //MakeNewestStatistics();


            //foreach (var v in new List<string>{ "1ad1cdc0-3ab8-4bbc-aff4-b7f100ae7f9c","06a33dc3-0f28-4a5a-a1a4-0db050153a60","1e70609f-891b-477e-a8bd-dd68511eaf76","10a1f69c-1628-4add-8f1b-dd8a280d643e","1e70609f-891b-477e-a8bd-dd68511eaf76","eb96e4f0-2f35-4ed6-9c21-f038e4f77927","4bf832d8-3784-43ff-8320-1da52888125a","ed4e0228-7006-4c90-8db1-7dcabb25d7d1","22f28122-248e-4a35-bedc-54221ae0a4e5","3fc86326-1778-4289-9215-03d536f6be4e","49c5f834-61e0-481a-9928-75cdcb5cce00","f751e73a-197a-4c0f-9bbd-959b8fe2c542","ef8acbd0-1614-43ae-bf43-751c1de69463","8e531a81-37ff-4f52-bbec-15235168f63f","cdcb11bb-0e3d-45f5-9860-d43c2cd8cf8a","388ea970-46be-4fb6-a711-775976682f83","f92b83d9-ea09-4871-8a9e-c1da588f1cfc","c6c7b913-a729-4537-b105-5bb50169525d","dcdb1c84-0eff-4369-8f94-d7278045ba5d","b3587e9d-0f08-42e5-8d65-92b29c6781cf","4226329f-9fa7-4597-844b-a7f01e5bf66e","31f6e897-b0e4-4bc7-82fb-9f4593198ffd","7b01ea86-229a-457d-8af7-44459b9b170b","d3363d7c-087c-4a4b-8fff-95c779b0815f","99e45e66-57ee-4ad3-a40d-1f02974898f1","35f369eb-0cb1-44f9-9934-7a902ff7bf34","986ec690-5e4a-4b0b-b9a3-b2fca627bfb4","dd7f482e-e180-4846-9570-0f54fd25ffbd","dcdb1c84-0eff-4369-8f94-d7278045ba5d","f7aded5a-05a7-491d-94a6-1756d454d3ba","1e70609f-891b-477e-a8bd-dd68511eaf76","0a9cbba7-c66a-4ac5-80a5-6f5c4fd735f8","15275572-bd30-48c2-85f2-f4607e6b99e4","c70a72bc-187e-41d5-93a7-888420d3ade5","cb64fe85-d0ae-4372-8aaf-7a5f80ad448a","71847c37-69f2-4f01-9b8d-1895d4699b98","4226329f-9fa7-4597-844b-a7f01e5bf66e","71847c37-69f2-4f01-9b8d-1895d4699b98","1e70609f-891b-477e-a8bd-dd68511eaf76","10a1f69c-1628-4add-8f1b-dd8a280d643e","0bb9a56b-8f06-4316-a972-36b3ba2d55ee","183e8053-8287-45a5-8c7a-4c88da76797c","7dde6604-5252-4137-9054-bc82a433fb08","587d5c81-2431-4b3a-b9b8-f996ef1a144b","52211a9e-de91-4168-b697-c787a63c2874","b800091e-8c41-435b-a141-d15b34b1040f","0c26d70e-e5b1-4673-a371-262ff094517d","06a33dc3-0f28-4a5a-a1a4-0db050153a60","b501133f-0bae-4a51-a8e5-6b613f3be5ef","c1052acf-c8c8-4d7b-bbf5-cd7131c9cbcb","f92b83d9-ea09-4871-8a9e-c1da588f1cfc","1209b7ed-47cc-4efb-a0ee-d8cb62600d08","1e70609f-891b-477e-a8bd-dd68511eaf76","10e0e29d-7d2f-4516-8d51-581ad9ccedb5","f09532fd-0731-41fb-b0a1-e8d13039560c","dd7f482e-e180-4846-9570-0f54fd25ffbd","15275572-bd30-48c2-85f2-f4607e6b99e4","33e92193-878e-4fbb-852c-a4a797b05ce4","ee47bbea-c562-4d45-bb8f-0c45205b5c20","1865b9f1-6b99-4109-9824-a9a3b0720381","21caf6e5-79c6-494e-bd6c-a44f4533eaad","02d9d47f-77b0-4e0d-8ef0-c5d1d9076526","751c2eca-3952-4fee-934a-3337b70bc6d6","cb64fe85-d0ae-4372-8aaf-7a5f80ad448a","dcdb1c84-0eff-4369-8f94-d7278045ba5d","b3587e9d-0f08-42e5-8d65-92b29c6781cf","b041a24c-24d4-4184-9bb5-c253fbfc21ca","b34e3f33-7593-4f57-b9f9-c337af53196c","f4fa9204-66db-4b12-a96f-33f4a3427962","c1052acf-c8c8-4d7b-bbf5-cd7131c9cbcb","1e70609f-891b-477e-a8bd-dd68511eaf76","c5105ae7-eb08-4a09-af6c-341d840fb8df","e0275ab1-98d3-45dd-8e3c-7b2b55ff777a","6c175125-d689-4e19-8949-690552fe1223","2a36a36d-5119-42b5-88e7-e24cfed878fd","1d680a89-9dc0-4cb6-9b17-b70be20bc1dd","b501133f-0bae-4a51-a8e5-6b613f3be5ef","c1052acf-c8c8-4d7b-bbf5-cd7131c9cbcb","1e70609f-891b-477e-a8bd-dd68511eaf76","10e0e29d-7d2f-4516-8d51-581ad9ccedb5","670f9346-91d0-4fee-bacd-5815d6062874","f944124d-f30c-43b3-ac49-f8413f965b8a","7b01ea86-229a-457d-8af7-44459b9b170b","b800091e-8c41-435b-a141-d15b34b1040f","ef7038e8-8d4a-4540-8a78-afc6b53cb81e","c70a72bc-187e-41d5-93a7-888420d3ade5","b501133f-0bae-4a51-a8e5-6b613f3be5ef","7c4458db-b662-49e5-9816-64eeb9a69063","751c2eca-3952-4fee-934a-3337b70bc6d6","8e531a81-37ff-4f52-bbec-15235168f63f","b2d96dea-04e5-4d7b-998a-84f49807846b","6fc45b8d-705c-43a2-bae1-2b5af2d6941a","0bb9a56b-8f06-4316-a972-36b3ba2d55ee","f949ee70-463a-4a8a-846f-20a44c6df1e7","98b519e3-c22b-4d6f-aaf9-f272243981c2","d4494c28-a094-40c0-9e76-f60376296b9e","c9adeeae-458d-433c-ad59-33f04a6d51a6","011a0cfe-0749-4770-b027-fa4e5ae1a916","0771a937-9711-4212-9111-845349c68357","368db349-674c-4df7-8f14-e334a5db30ac","0bb9a56b-8f06-4316-a972-36b3ba2d55ee","31a48bef-02a0-455d-b381-29a804a6ac5f","3ed27df6-8aae-4f2f-804c-3db54e93284f","efe33b3b-1b55-466c-8ea1-c8e1bc10a655","0771a937-9711-4212-9111-845349c68357","d5a6c3f2-7472-42df-bdbc-b9d3d5c42209","16596897-5cd3-4062-bd61-69e23ed06df7","8b93d432-c572-4aa1-b978-b863501a4788","0a5bd4ce-a2dd-4f80-82ac-69f301828d80"}) 
            //    ReadUsersLandfields(v);
            //ReadAllUsersLandfields();
        }
    }
}
