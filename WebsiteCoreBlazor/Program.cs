using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WebsiteCoreBlazor.Data;

namespace WebsiteCoreBlazor
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var uids = new HashSet<string>();
            /* EXTRACT IMPORTANT USER IDS FOR DETAILS CODE
            var db = new E2DB();
            var ss = new SimpleStatsService();
            
            foreach (var s in ss.GetBearStats())
                uids.Add(s.user.Id);
            foreach (var s in ss.GetBullStats())
                uids.Add(s.user.Id);
            foreach (var s in ss.GetChaserStats())
                uids.Add(s.user.Id);
            foreach (var s in ss.GetDrawerStats())
                uids.Add(s.user.Id);
            foreach (var s in ss.GetFishStats())
                uids.Add(s.user.Id);
            foreach (var s in ss.GetHodlerStats())
                uids.Add(s.user.Id);
            foreach (var s in ss.GetSelloutStats())
                uids.Add(s.user.Id);
            foreach (var s in ss.GetShimmerStats())
                uids.Add(s.user.Id);
            foreach (var s in ss.GetUnicornStats())
                uids.Add(s.user.Id);

            var r = "{";
            foreach(var id in uids)
            {
                r = r + "\"" + id + "\",";

            }
            r = r + "}";

            return;
            */

            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
