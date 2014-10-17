using System;
using System.Text;
using System.Collections.Generic;


namespace SalonCrawler {
    
    public partial class Post {
        public Post() { }
        public virtual int Id { get; set; }
        public virtual User User { get; set; }
        public virtual Category Category { get; set; }
        public virtual string Title { get; set; }
        public virtual DateTime Date { get; set; }
        public virtual int? ViewCount { get; set; }
        public virtual int? CommentCount { get; set; }
        public virtual string PostContent { get; set; }
        public virtual int? FacebookLikes { get; set; }
        public virtual int? TwitterLikes { get; set; }
        public virtual int? GoogleLikes { get; set; }
        public virtual int? WykopLikes { get; set; }
        public virtual string Tags { get; set; }
        public virtual DateTime? LastUpdatedOn { get; set; }
        public virtual IList<Comment> Comments { get; set; }
        public virtual IList<Repost> Reposts { get; set; }
    }
}
