using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebsiteCoreBlazor.Data
{
    public class SimpleStatsService
    {
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

        public List<DateTime> GetMomenta()
        {
            E2DB db = new E2DB();
            return db.Simpletons.Select(d => d.momenta)
                                .Distinct()
                                .AsEnumerable()
                                .OrderByDescending(d=>d.Ticks)
                                .ToList();
        }



        private List<SimpleStatsViewModel> GetTop100(IQueryable<SimpleStatsViewModel> q)
        {
            return q.Take(50).ToList();
        }
        public List<SimpleStatsViewModel> GetPortfolioSize(DateTime moment)
        {
            E2DB db = new E2DB();
            var q =
                from data in db.Simpletons
                join user in db.Users
                on data.userid equals user.Id
                where data.totalPropertiesOwned > 0
                    && data.momenta == moment
                orderby data.currentPropertiesOwned descending
                select new SimpleStatsViewModel
                {
                    user = user,
                    datavalue = formatBigNumbers(data.currentPropertiesOwned)
                };
            return GetTop100(q);
        }

        public List<SimpleStatsViewModel> GetPortfolioWeight(DateTime moment)
        {
            E2DB db = new E2DB();
            var q =
                from data in db.Simpletons
                join user in db.Users
                on data.userid equals user.Id
                let port_weight = data.tilesCurrentlyOwned / (float)data.totalPropertiesOwned
                where data.totalPropertiesOwned > 25
                    && data.momenta == moment
                orderby port_weight descending
                select new SimpleStatsViewModel
                {
                    user = user,
                    datavalue = port_weight.ToString("N1")
                };
            return GetTop100(q);
        }

        public List<SimpleStatsViewModel> GetBearStats(DateTime moment)
        {
            if (moment < new DateTime(2021, 1, 1))
                return new List<SimpleStatsViewModel>();
            E2DB db = new E2DB();
            var q =
                from data in db.Simpletons
                join user in db.Users
                on data.userid equals user.Id
                let bear_bull = (float)data.tilesSoldAmount / data.tilesBoughtAmount
                where (data.tilesBoughtAmount + data.tilesSoldAmount >= 9000)
                    && data.momenta == moment
                orderby bear_bull descending, data.tilesBoughtAmount descending
                select new SimpleStatsViewModel
                {
                    user = user,
                    //datavalue = bear_bull.ToString("N5")
                    datavalue = (bear_bull * 100).ToString("N1") + "%"
                };
            return GetTop100(q);
        }

        public List<SimpleStatsViewModel> GetBullStats(DateTime moment)
        {
            if (moment < new DateTime(2021, 1, 1))
                return new List<SimpleStatsViewModel>();
            E2DB db = new E2DB();
            var top =
            (from data in db.Simpletons
             where data.totalPropertiesOwned >= 50
             orderby data.tilesBoughtAmount descending
             select data.tilesBoughtAmount).Take(1).ToList()[0];

            var q =
                from data in db.Simpletons
                join user in db.Users
                on data.userid equals user.Id
                let bear_bull = (float)data.tilesSoldAmount / data.tilesBoughtAmount
                where (data.tilesBoughtAmount + data.tilesSoldAmount >= 9000)
                    && data.momenta == moment
                orderby bear_bull ascending, data.tilesBoughtAmount descending
                select new SimpleStatsViewModel
                {
                    user = user,
                    //datavalue = bear_bull.ToString("N5")
                    datavalue = ((1-bear_bull) * 100).ToString("N1") + "%"
                };
            return GetTop100(q);
        }

        public List<SimpleStatsViewModel> GetSelloutStats(DateTime moment)
        {
            E2DB db = new E2DB();
            var q =
                from data in db.Simpletons
                join user in db.Users
                on data.userid equals user.Id
                let sellout_hodler = data.tilesCurrentlyOwned - data.tilesSoldAmount
                where true
                    && data.momenta == moment
                orderby sellout_hodler ascending
                select new SimpleStatsViewModel
                {
                    user = user,
                    datavalue = sellout_hodler.ToString()
                };
            return GetTop100(q);
        }

        public List<SimpleStatsViewModel> GetHodlerStats(DateTime moment)
        {
            E2DB db = new E2DB();
            var q =
                from data in db.Simpletons
                join user in db.Users
                on data.userid equals user.Id
                let sellout_hodler = data.tilesCurrentlyOwned - data.tilesSoldAmount
                where true
                    && data.momenta == moment
                orderby sellout_hodler descending
                select new SimpleStatsViewModel
                {
                    user = user,
                    datavalue = sellout_hodler.ToString()
                };
            return GetTop100(q);
        }


        public List<SimpleStatsViewModel> GetUnicornStats(DateTime moment)
        {
            E2DB db = new E2DB();
            var q =
                from data in db.Simpletons
                join user in db.Users
                on data.userid equals user.Id
                let unicorn = data.profitsOnSell
                where true
                    && data.momenta == moment
                orderby unicorn descending
                select new SimpleStatsViewModel
                {
                    user = user,
                    datavalue = "$ " + formatBigNumbers(unicorn)
                };
            return GetTop100(q);
        }

        public List<SimpleStatsViewModel> GetFishStats(DateTime moment)
        {
            E2DB db = new E2DB();
            var q =
                from data in db.Simpletons
                join user in db.Users
                on data.userid equals user.Id
                let fish_drawer = (float) data.profitsOnSell / (float)data.totalPropertiesResold
                where data.totalPropertiesResold >= 100
                    && data.momenta == moment
                orderby fish_drawer descending
                select new SimpleStatsViewModel
                {
                    user = user,
                    datavalue = formatBigNumbers(fish_drawer)
                };
            return GetTop100(q);
        }

        public List<SimpleStatsViewModel> GetDrawerStats(DateTime moment)
        {
            E2DB db = new E2DB();
            var q =
                from data in db.Simpletons
                join user in db.Users
                on data.userid equals user.Id
                let fish_drawer = (float)data.profitsOnSell / (float)data.totalPropertiesResold
                where data.totalPropertiesResold >= 100
                    && data.momenta == moment
                orderby fish_drawer ascending
                select new SimpleStatsViewModel
                {
                    user = user,
                    datavalue = formatBigNumbers(fish_drawer)
                };
            return GetTop100(q);
        }

        public List<SimpleStatsViewModel> GetChaserStats(DateTime moment)
        {
            E2DB db = new E2DB();
            var q =
                from data in db.Simpletons
                join user in db.Users
                on data.userid equals user.Id
                let ret_ret = data.returnsOnSell / data.totalPropertiesResold
                let resoldratio = data.totalPropertiesResold / (float)data.totalPropertiesOwned
                where data.totalUniquePropertiesOwned >= 60 && data.totalPropertiesResold >= 60
                    && data.momenta == moment
                orderby ret_ret descending
                select new SimpleStatsViewModel
                {
                    user = user,
                    datavalue = (ret_ret * 100).ToString("N1") + "%"
                };
            return GetTop100(q);
        }

        public List<SimpleStatsViewModel> GetShimmerStats(DateTime moment)
        {
            E2DB db = new E2DB();
            var q =
                from data in db.Simpletons
                join user in db.Users
                on data.userid equals user.Id
                let ret_ret = data.returnsOnSell / data.totalPropertiesResold
                let resoldratio = data.totalPropertiesResold / (float)data.totalPropertiesOwned
                where data.totalUniquePropertiesOwned >= 60 && data.totalPropertiesResold >= 60
                    && data.momenta == moment
                orderby ret_ret ascending
                select new SimpleStatsViewModel
                {
                    user = user,
                    datavalue = (ret_ret * 100).ToString("N1") + "%"
                };
            return GetTop100(q);
        }


        public List<SimpleStatsViewModel> COMPLETETESTGETMETHOD(DateTime moment)
        {
            E2DB db = new E2DB();
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
                where true
                    && data.momenta == moment
                orderby bear_bull ascending
                select new SimpleStatsViewModel
                {
                    user = user,
                    datavalue = bear_bull.ToString()
                };
            return GetTop100(q);
        }
    }
}
