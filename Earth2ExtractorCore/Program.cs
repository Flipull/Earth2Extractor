using E2ExCoreLibrary;
using E2ExCoreLibrary.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Earth2ExtractorCore
{
    class Program
    {

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
            if (db.LandFields.Find(new_field.id) == null)
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
                        db.Transactions.Add(t);
                    }
                }
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
                int page = r.Next(0,9);
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

        static void ReadAllUsersLandfields()
        {
            var r = new Random();
            var db = new E2DB();

            while(true)
            {

                var user = db.Users.OrderBy(u => u.updated)
                                    .ThenByDescending(u => u.name)
                                    .Where(u => !u.locked &&
                                                (!u.updated.HasValue || u.updated.Value <= DateTime.Now.AddDays(-2) ) )
                                    .First();
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



        ////////////////////////////////////////////
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            Console.ReadLine();
            //E2Reader.ReadLandField("5d24b96b-0917-4c72-9f4a-84a7ce4d3e77");
            ReadAllMarketPlaceOffers();
            //ReadAllLeaderboardUsers();
            ReadAllUsersLandfields();
        }
    }
}
