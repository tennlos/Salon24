using NHibernate;
using NHibernate.Cfg;

namespace SalonCrawler
{
    public sealed class NHibernateHelper
    {
        private static ISessionFactory _sessionFactory;

        private static void OpenSession()
        {
            var configuration = new Configuration();
            configuration.Configure();
            _sessionFactory = configuration.BuildSessionFactory();
        }

        public static ISession GetCurrentSession()
        {
            if (_sessionFactory == null)
                OpenSession();

            return _sessionFactory.OpenSession();
        }

        public static void CloseSessionFactory()
        {
            if (_sessionFactory != null)
                _sessionFactory.Close();
        }
    }
}
