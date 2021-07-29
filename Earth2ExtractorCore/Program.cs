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
                Console.WriteLine("Leaderboards ("+c+")");
                AddUsersToDB(E2Reader.ReadLeaderboardUsers(c, "networth"));
                AddUsersToDB(E2Reader.ReadLeaderboardUsers(c, "tiles"));
            }
        }
        static void UpdateAllUsers()//name, country
        {
            
        }
        static void ReadAllMarketPlaceOffers()
        {
            var r = new Random();
            Constants.Countries.Shuffle();
            foreach(var c in Constants.Countries)
            {
                Console.WriteLine("MarketPlace (" + c + ")");
                int resultcount = 25;
                int page = r.Next(0,19);
                string sort = Constants.MPSorting[r.Next(0, 4)];
                var fields = E2Reader.ReadMarketPlace(c, page, sort);

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
                                                (!u.updated.HasValue || u.updated.Value <= DateTime.Now.AddHours(-32) ) )
                                    .Take(20).ToList();
                var user = userlist[r.Next(0, userlist.Count-1)];//crashes on empty list
                user.locked = true;
                db.SaveChanges();

                var fieldsdata = E2Reader.ReadUserLandFieldsV(user.Id, 1);
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
                Thread.Sleep(10000);
            }
            
        }


        static void ReadAllUserDetails()
        {
            var db = new E2DB();
            
            foreach (var user in db.Users.ToList())
            {
                var userdata = E2Reader.ReadUser(user.Id);
                user.customPhoto = userdata.customPhoto;
                user.countryCode = userdata.countryCode;
                user.name = userdata.username;
            }
        }

        ////////////////////////////////////////////
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            Console.ReadLine();

            ReadAllUserDetails();

            //MakeNewestStatistics();
            
            //foreach (var v in new List<string>{ "1ad1cdc0-3ab8-4bbc-aff4-b7f100ae7f9c","06a33dc3-0f28-4a5a-a1a4-0db050153a60","1e70609f-891b-477e-a8bd-dd68511eaf76","10a1f69c-1628-4add-8f1b-dd8a280d643e","1e70609f-891b-477e-a8bd-dd68511eaf76","eb96e4f0-2f35-4ed6-9c21-f038e4f77927","4bf832d8-3784-43ff-8320-1da52888125a","ed4e0228-7006-4c90-8db1-7dcabb25d7d1","22f28122-248e-4a35-bedc-54221ae0a4e5","3fc86326-1778-4289-9215-03d536f6be4e","49c5f834-61e0-481a-9928-75cdcb5cce00","f751e73a-197a-4c0f-9bbd-959b8fe2c542","ef8acbd0-1614-43ae-bf43-751c1de69463","8e531a81-37ff-4f52-bbec-15235168f63f","cdcb11bb-0e3d-45f5-9860-d43c2cd8cf8a","388ea970-46be-4fb6-a711-775976682f83","f92b83d9-ea09-4871-8a9e-c1da588f1cfc","c6c7b913-a729-4537-b105-5bb50169525d","dcdb1c84-0eff-4369-8f94-d7278045ba5d","b3587e9d-0f08-42e5-8d65-92b29c6781cf","4226329f-9fa7-4597-844b-a7f01e5bf66e","31f6e897-b0e4-4bc7-82fb-9f4593198ffd","7b01ea86-229a-457d-8af7-44459b9b170b","d3363d7c-087c-4a4b-8fff-95c779b0815f","99e45e66-57ee-4ad3-a40d-1f02974898f1","35f369eb-0cb1-44f9-9934-7a902ff7bf34","986ec690-5e4a-4b0b-b9a3-b2fca627bfb4","dd7f482e-e180-4846-9570-0f54fd25ffbd","dcdb1c84-0eff-4369-8f94-d7278045ba5d","f7aded5a-05a7-491d-94a6-1756d454d3ba","1e70609f-891b-477e-a8bd-dd68511eaf76","0a9cbba7-c66a-4ac5-80a5-6f5c4fd735f8","15275572-bd30-48c2-85f2-f4607e6b99e4","c70a72bc-187e-41d5-93a7-888420d3ade5","cb64fe85-d0ae-4372-8aaf-7a5f80ad448a","71847c37-69f2-4f01-9b8d-1895d4699b98","4226329f-9fa7-4597-844b-a7f01e5bf66e","71847c37-69f2-4f01-9b8d-1895d4699b98","1e70609f-891b-477e-a8bd-dd68511eaf76","10a1f69c-1628-4add-8f1b-dd8a280d643e","0bb9a56b-8f06-4316-a972-36b3ba2d55ee","183e8053-8287-45a5-8c7a-4c88da76797c","7dde6604-5252-4137-9054-bc82a433fb08","587d5c81-2431-4b3a-b9b8-f996ef1a144b","52211a9e-de91-4168-b697-c787a63c2874","b800091e-8c41-435b-a141-d15b34b1040f","0c26d70e-e5b1-4673-a371-262ff094517d","06a33dc3-0f28-4a5a-a1a4-0db050153a60","b501133f-0bae-4a51-a8e5-6b613f3be5ef","c1052acf-c8c8-4d7b-bbf5-cd7131c9cbcb","f92b83d9-ea09-4871-8a9e-c1da588f1cfc","1209b7ed-47cc-4efb-a0ee-d8cb62600d08","1e70609f-891b-477e-a8bd-dd68511eaf76","10e0e29d-7d2f-4516-8d51-581ad9ccedb5","f09532fd-0731-41fb-b0a1-e8d13039560c","dd7f482e-e180-4846-9570-0f54fd25ffbd","15275572-bd30-48c2-85f2-f4607e6b99e4","33e92193-878e-4fbb-852c-a4a797b05ce4","ee47bbea-c562-4d45-bb8f-0c45205b5c20","1865b9f1-6b99-4109-9824-a9a3b0720381","21caf6e5-79c6-494e-bd6c-a44f4533eaad","02d9d47f-77b0-4e0d-8ef0-c5d1d9076526","751c2eca-3952-4fee-934a-3337b70bc6d6","cb64fe85-d0ae-4372-8aaf-7a5f80ad448a","dcdb1c84-0eff-4369-8f94-d7278045ba5d","b3587e9d-0f08-42e5-8d65-92b29c6781cf","b041a24c-24d4-4184-9bb5-c253fbfc21ca","b34e3f33-7593-4f57-b9f9-c337af53196c","f4fa9204-66db-4b12-a96f-33f4a3427962","c1052acf-c8c8-4d7b-bbf5-cd7131c9cbcb","1e70609f-891b-477e-a8bd-dd68511eaf76","c5105ae7-eb08-4a09-af6c-341d840fb8df","e0275ab1-98d3-45dd-8e3c-7b2b55ff777a","6c175125-d689-4e19-8949-690552fe1223","2a36a36d-5119-42b5-88e7-e24cfed878fd","1d680a89-9dc0-4cb6-9b17-b70be20bc1dd","b501133f-0bae-4a51-a8e5-6b613f3be5ef","c1052acf-c8c8-4d7b-bbf5-cd7131c9cbcb","1e70609f-891b-477e-a8bd-dd68511eaf76","10e0e29d-7d2f-4516-8d51-581ad9ccedb5","670f9346-91d0-4fee-bacd-5815d6062874","f944124d-f30c-43b3-ac49-f8413f965b8a","7b01ea86-229a-457d-8af7-44459b9b170b","b800091e-8c41-435b-a141-d15b34b1040f","ef7038e8-8d4a-4540-8a78-afc6b53cb81e","c70a72bc-187e-41d5-93a7-888420d3ade5","b501133f-0bae-4a51-a8e5-6b613f3be5ef","7c4458db-b662-49e5-9816-64eeb9a69063","751c2eca-3952-4fee-934a-3337b70bc6d6","8e531a81-37ff-4f52-bbec-15235168f63f","b2d96dea-04e5-4d7b-998a-84f49807846b","6fc45b8d-705c-43a2-bae1-2b5af2d6941a","0bb9a56b-8f06-4316-a972-36b3ba2d55ee","f949ee70-463a-4a8a-846f-20a44c6df1e7","98b519e3-c22b-4d6f-aaf9-f272243981c2","d4494c28-a094-40c0-9e76-f60376296b9e","c9adeeae-458d-433c-ad59-33f04a6d51a6","011a0cfe-0749-4770-b027-fa4e5ae1a916","0771a937-9711-4212-9111-845349c68357","368db349-674c-4df7-8f14-e334a5db30ac","0bb9a56b-8f06-4316-a972-36b3ba2d55ee","31a48bef-02a0-455d-b381-29a804a6ac5f","3ed27df6-8aae-4f2f-804c-3db54e93284f","efe33b3b-1b55-466c-8ea1-c8e1bc10a655","0771a937-9711-4212-9111-845349c68357","d5a6c3f2-7472-42df-bdbc-b9d3d5c42209","16596897-5cd3-4062-bd61-69e23ed06df7","8b93d432-c572-4aa1-b978-b863501a4788","0a5bd4ce-a2dd-4f80-82ac-69f301828d80"}) 
            //    ReadUsersLandfields(v);
            
            //E2Reader.ReadLandField("5d24b96b-0917-4c72-9f4a-84a7ce4d3e77");
            
            //while (true)
            //    ReadAllMarketPlaceOffers();
            
            //ReadAllLeaderboardUsers();
            
            //ReadAllUsersLandfields();
        }
    }
}
