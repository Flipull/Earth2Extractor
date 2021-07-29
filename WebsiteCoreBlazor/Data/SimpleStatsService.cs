using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebsiteCoreBlazor.Data
{
    public class SimpleStatsService
    {
        private E2DB db = new E2DB();
        private static char[] exponentsbig = new char[] { ' ', 'K', 'M', 'B', 'T', '?', '?', '?' };
        private static string formatBigNumbers(Decimal n)
        {
            if (n < 0)
                return "-" + formatBigNumbers(-n);

            int exp = 0;
            while (n > 1000)
            {
                n /= 1000;
                exp += 1;
            }

            if (exp > 0) return n.ToString("N1") + exponentsbig[exp];
            return n.ToString("N2");
        }
        private static string formatBigNumbers(int n)
        {
            if (n < 0)
                return "-" + formatBigNumbers(-n);

            float m = n;
            int exp = 0;
            while (m > 1000)
            {
                m /= 1000;
                exp += 1;
            }
            
            if (exp > 0) return m.ToString("N1") + exponentsbig[exp];
            return m.ToString("N0");
        }
        private static string formatBigNumbers(double n)
        {
            if (n < 0)
                return "-" + formatBigNumbers(-n);

            int exp = 0;
            while (n > 1000)
            {
                n /= 1000;
                exp += 1;
            }
            if (exp > 0) return n.ToString("N1") + exponentsbig[exp];
            return n.ToString("N2");
        }

        public List<SimpleStatsViewModel> GetBearStats()
        {
            var q =
                from data in db.Simpletons
                join user in db.Users
                on data.userid equals user.Id
                let bear_bull = data.tilesSoldAmount / (float)data.tilesBoughtAmount
                where data.totalPropertiesOwned >= 50
                orderby bear_bull descending
                select new SimpleStatsViewModel
                {
                    user = user,
                    datavalue = (bear_bull * 100).ToString("N1") + "%"
                };
            return q.Take(100).ToList();
        }

        public List<SimpleStatsViewModel> GetBullStats()
        {
            var q =
                from data in db.Simpletons
                join user in db.Users
                on data.userid equals user.Id
                let bear_bull = data.tilesSoldAmount / (float)data.tilesBoughtAmount
                where data.totalPropertiesOwned >= 50
                orderby bear_bull ascending
                select new SimpleStatsViewModel
                {
                    user = user,
                    datavalue = ((1-bear_bull) * 100).ToString("N1") + "%"
                };
            return q.Take(100).ToList();
        }
        
        public List<SimpleStatsViewModel> GetSelloutStats()
        {
            var q =
                from data in db.Simpletons
                join user in db.Users
                on data.userid equals user.Id
                let sellout_hodler = data.tilesCurrentlyOwned - data.tilesSoldAmount
                orderby sellout_hodler ascending
                select new SimpleStatsViewModel
                {
                    user = user,
                    datavalue = sellout_hodler.ToString()
                };
            return q.Take(100).ToList();
        }

        public List<SimpleStatsViewModel> GetHodlerStats()
        {
            var q =
                from data in db.Simpletons
                join user in db.Users
                on data.userid equals user.Id
                let sellout_hodler = data.tilesCurrentlyOwned - data.tilesSoldAmount
                orderby sellout_hodler descending
                select new SimpleStatsViewModel
                {
                    user = user,
                    datavalue = sellout_hodler.ToString()
                };
            return q.Take(100).ToList();
        }


        public List<SimpleStatsViewModel> GetFishStats()
        {
            var q =
                from data in db.Simpletons
                join user in db.Users
                on data.userid equals user.Id
                let fish_drawer = data.profitsOnSell
                orderby fish_drawer descending
                select new SimpleStatsViewModel
                {
                    user = user,
                    datavalue = formatBigNumbers(fish_drawer)
                };
            return q.Take(100).ToList();
        }

        public List<SimpleStatsViewModel> GetDrawerStats()
        {
            var q =
                from data in db.Simpletons
                join user in db.Users
                on data.userid equals user.Id
                let fish_drawer = data.profitsOnSell
                orderby fish_drawer ascending
                select new SimpleStatsViewModel
                {
                    user = user,
                    datavalue = formatBigNumbers(fish_drawer)
                };
            return q.Take(100).ToList();
        }

        public List<SimpleStatsViewModel> GetChaserStats()
        {
            var q =
                from data in db.Simpletons
                join user in db.Users
                on data.userid equals user.Id
                let ret_ret = data.returnsOnSell / data.totalPropertiesResold
                let resoldratio = data.totalPropertiesResold / (float)data.totalPropertiesOwned
                where data.totalPropertiesResold >= 25
                orderby ret_ret descending
                select new SimpleStatsViewModel
                {
                    user = user,
                    datavalue = (ret_ret * 100).ToString("N1") + "%"
                };
            return q.Take(100).ToList();
        }

        public List<SimpleStatsViewModel> GetShimmerStats()
        {
            var q =
                from data in db.Simpletons
                join user in db.Users
                on data.userid equals user.Id
                let ret_ret = data.returnsOnSell / data.totalPropertiesResold
                let resoldratio = data.totalPropertiesResold / (float)data.totalPropertiesOwned
                where data.totalPropertiesResold >= 25
                orderby ret_ret ascending
                select new SimpleStatsViewModel
                {
                    user = user,
                    datavalue = (ret_ret * 100).ToString("N1") + "%"
                };
            return q.Take(100).ToList();
        }

        public List<SimpleStatsViewModel> GetPortfolioSize()
        {
            var q =
                from data in db.Simpletons
                join user in db.Users
                on data.userid equals user.Id
                where data.totalPropertiesOwned > 0
                orderby data.currentPropertiesOwned descending
                select new SimpleStatsViewModel
                {
                    user = user,
                    datavalue = formatBigNumbers(data.currentPropertiesOwned)
                };
            return q.Take(100).ToList();
        }
        
        public List<SimpleStatsViewModel> GetPortfolioWeight()
        {
            var q =
                from data in db.Simpletons
                join user in db.Users
                on data.userid equals user.Id
                let port_weight = data.tilesCurrentlyOwned / (float)data.totalPropertiesOwned
                where data.totalPropertiesOwned > 25
                orderby port_weight descending
                select new SimpleStatsViewModel
                {
                    user = user,
                    datavalue = port_weight.ToString("N1")
                };
            return q.Take(100).ToList();
        }

        public List<SimpleStatsViewModel> COMPLETETESTGETMETHOD()
        {
            var q =
                from data in db.Simpletons
                join user in db.Users
                on data.userid equals user.Id
                let sellout_hodler = data.tilesCurrentlyOwned - data.tilesSoldAmount
                let bear_bull = data.tilesSoldAmount / (float)data.tilesBoughtAmount
                let fish_drawer = data.profitsOnSell
                let profit_loss = data.profitsOnSell / data.totalPropertiesResold
                let ret_ret = data.returnsOnSell / data.totalPropertiesResold

                let resoldratio = data.totalPropertiesResold / (float)data.totalPropertiesOwned
                //where ???????????
                orderby bear_bull ascending
                select new SimpleStatsViewModel
                {
                    user = user,
                    datavalue = bear_bull.ToString()
                };
            return q.Take(100).ToList();
        }
    }
}
