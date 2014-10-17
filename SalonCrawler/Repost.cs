using System;
using System.Text;
using System.Collections.Generic;


namespace SalonCrawler {
    
    public partial class Repost {
        public virtual int Id { get; set; }
        public virtual Post Post { get; set; }
        public virtual Newspaper Newspaper { get; set; }
    }
}
