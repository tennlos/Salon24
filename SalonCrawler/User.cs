using System;
using System.Text;
using System.Collections.Generic;


namespace SalonCrawler {
    
    public partial class User {
        public User() {
            this.Newspapers = new List<Newspaper>();
        }
        public virtual int Id { get; set; }
        public virtual string Nick { get; set; }
        public virtual string AboutMe { get; set; }
        public virtual string Description { get; set; }
        public virtual string Address { get; set; }
        public virtual int? PostCount { get; set; }
        public virtual int? CommentCount { get; set; }
        public virtual DateTime? LastUpdatedOn { get; set; }
        public virtual IList<Post> Posts { get; set; }
        public virtual IList<Newspaper> Newspapers { get; set; }
        public virtual int? Type { get; set; }
    }
}
