﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


using workIT.Factories;
using workIT.Models;
using workIT.Models.Common;
using workIT.Models.Helpers.Reports;

namespace workIT.Services
{
	public class ReportServices
	{
		public static CommonTotals SiteTotals()
		{
            var currentDate = DateTime.Now;
            currentDate = currentDate.AddDays( -2 );
			CommonTotals totals = ActivityManager.SiteTotals_Get();
			totals.MainEntityTotals = MainEntityTotals();
            totals.CredentialHistory = HistoryReports( 1);
            totals.OrganizationHistory = HistoryReports( 2 );
            totals.AssessmentHistory = HistoryReports( 3 );
            totals.LearningOpportunityHistory = ReportServices.HistoryReports( 7 );

			totals.EntityRegionTotals = CodesManager.GetEntityRegionTotals( 1, "United States");
			//vm.TotalDirectCredentials = list.FirstOrDefault( x => x.Id == 1 ).Totals;
			//vm.TotalOrganizations = list.FirstOrDefault( x => x.Id == 2 ).Totals;
			//vm.TotalQAOrganizations = list.FirstOrDefault( x => x.Id == 99 ).Totals;
			totals.AgentServiceTypes = new EnumerationServices().GetOrganizationServices(EnumerationType.MULTI_SELECT, false);

			totals.PropertiesTotals = PropertyTotals();
			//
			//get totals from view: CodesProperty_Counts_ByEntity.
			//	the latter has a union with Counts.SiteTotals
			totals.PropertiesTotalsByEntity = CodesManager.Property_GetTotalsByEntity();
            totals.PropertiesTotals.AddRange( CodesManager.GetAllEntityStatistics());
            //using counts.SiteTotals - so based on the above, this should not be needed???
            //var allSiteTotals = CodesManager.CodeEntity_GetCountsSiteTotals();
            //totals.SOC_Groups = allSiteTotals.Where( s => s.EntityTypeId == 1 && s.CategoryId == 11 ).ToList();
            //totals.CredentialIndustry_Groups = allSiteTotals.Where( s => s.EntityTypeId == 1 && s.CategoryId == 10 ).ToList();
            //totals.CredentialCIP_Groups = allSiteTotals.Where( s => s.EntityTypeId == 3 && s.CategoryId == 23 ).ToList();
            //totals.OrgIndustry_Groups = allSiteTotals.Where( s => s.EntityTypeId == 2 && s.CategoryId == 10 ).ToList();
            //totals.AssessmentCIP_Groups = allSiteTotals.Where( s => s.EntityTypeId == 3 && s.CategoryId == 23 ).ToList();
            //totals.LoppCIP_Groups = allSiteTotals.Where( s => s.EntityTypeId == 7 && s.CategoryId == 23 ).ToList();

            return totals;
		}

        public static List<HistoryTotal> HistoryReports( int entityTypeId )
        {
            var result = CodesManager.GetHistoryTotal( entityTypeId );
            return result;


        }

		/// <summary>
		/// Get Entity Codes with totals for Credential, Organization, assessments, and learning opp
		/// </summary>
		/// <returns></returns>
		public static List<CodeItem> MainEntityTotals()
		{
			List<CodeItem> list = CodesManager.CodeEntity_GetMainClassTotals();

			return list;
		}

		/// <summary>
		/// Get property totals, by category or all active properties
		/// </summary>
		/// <param name="categoryId"></param>
		/// <returns></returns>
		public static List<CodeItem> PropertyTotals( int categoryId = 0)
		{
			List<CodeItem> list = CodesManager.Property_GetSummaryTotals( categoryId );

			return list;
		}
	}
}
