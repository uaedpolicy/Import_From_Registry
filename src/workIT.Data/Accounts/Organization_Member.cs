//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated from a template.
//
//     Manual changes to this file may cause unexpected behavior in your application.
//     Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace workIT.Data.Accounts
{
    using System;
    using System.Collections.Generic;
    
    public partial class Organization_Member
    {
        public int Id { get; set; }
        public int ParentOrgId { get; set; }
        public int UserId { get; set; }
        public int OrgMemberTypeId { get; set; }
        public System.DateTime Created { get; set; }
        public Nullable<int> CreatedById { get; set; }
        public System.DateTime LastUpdated { get; set; }
        public Nullable<int> LastUpdatedById { get; set; }
        public Nullable<bool> IsPrimaryOrganization { get; set; }
    
        public virtual Account Account { get; set; }
        public virtual Organization Organization { get; set; }
    }
}
