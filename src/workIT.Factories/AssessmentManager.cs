﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using workIT.Models;
using workIT.Models.Common;
using workIT.Models.ProfileModels;
using workIT.Utilities;

using ThisEntity = workIT.Models.ProfileModels.AssessmentProfile;
using DBEntity = workIT.Data.Tables.Assessment;
using EntityContext = workIT.Data.Tables.workITEntities;
using ViewContext = workIT.Data.Views.workITViews;

using EM = workIT.Data.Tables;
using Views = workIT.Data.Views;
using CondProfileMgr = workIT.Factories.Entity_ConditionProfileManager;

namespace workIT.Factories
{
    public class AssessmentManager : BaseFactory
    {
        static readonly string thisClassName = "AssessmentManager";
        EntityManager entityMgr = new EntityManager();

        #region Assessment - persistance ==================
        /// <summary>
        /// Update a Assessment
        /// - base only, caller will handle parts?
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="status"></param>
        /// <returns></returns>
        public bool Save( ThisEntity entity, ref SaveStatus status )
        {
            bool isValid = true;
            int count = 0;
            try
            {
                using ( var context = new EntityContext() )
                {
                    if ( ValidateProfile( entity, ref status ) == false )
                        return false;

                    if ( entity.Id > 0 )
                    {
                        //TODO - consider if necessary, or interferes with anything
                        context.Configuration.LazyLoadingEnabled = false;
                        DBEntity efEntity = context.Assessment
                                .SingleOrDefault( s => s.Id == entity.Id );

                        if ( efEntity != null && efEntity.Id > 0 )
                        {
                            //delete the entity and re-add
                            //Entity e = new Entity()
                            //{
                            //    EntityBaseId = efEntity.Id,
                            //    EntityTypeId = CodesManager.ENTITY_TYPE_ASSESSMENT_PROFILE,
                            //    EntityType = "Assessment",
                            //    EntityUid = efEntity.RowId,
                            //    EntityBaseName = efEntity.Name
                            //};
                            //if ( entityMgr.ResetEntity( e, ref statusMessage ) )
                            //{

                            //}

                            //fill in fields that may not be in entity
                            entity.RowId = efEntity.RowId;

							MapToDB( entity, efEntity );

							//19-05-21 mp - should add a check for an update where currently is deleted
							if ( ( efEntity.EntityStateId ?? 0 ) == 0 )
							{
								var url = string.Format( UtilityManager.GetAppKeyValue( "credentialFinderSite" ) + "assessment/{0}", efEntity.Id );
								//notify, and???
								EmailManager.NotifyAdmin( "Previously Deleted Assessment has been reactivated", string.Format( "<a href='{2}'>Assessment: {0} ({1})</a> was deleted and has now been reactivated.", efEntity.Name, efEntity.Id, url ) );
								SiteActivity sa = new SiteActivity()
								{
									ActivityType = "AssessmentProfile",
									Activity = "Import",
									Event = "Reactivate",
									Comment = string.Format( "Assessment had been marked as deleted, and was reactivted by the import. Name: {0}, SWP: {1}", entity.Name, entity.SubjectWebpage ),
									ActivityObjectId = entity.Id
								};
								new ActivityManager().SiteActivityAdd( sa );
							}
							//assume and validate, that if we get here we have a full record
							if ( ( efEntity.EntityStateId ?? 1 ) != 2 )
								efEntity.EntityStateId = 3;

							if ( HasStateChanged( context ) )
							{
								efEntity.LastUpdated = System.DateTime.Now;
                                //NOTE efEntity.EntityStateId is set to 0 in delete method )
                                count = context.SaveChanges();
								//can be zero if no data changed
								if ( count >= 0 )
								{
									isValid = true;
								}
								else
								{
									//?no info on error
									
									isValid = false;
									string message = string.Format( thisClassName + ".Save Failed", "Attempted to update a Assessment. The process appeared to not work, but was not an exception, so we have no message, or no clue. Assessment: {0}, Id: {1}", entity.Name, entity.Id);
									status.AddError( "Error - the update was not successful. " + message );
									EmailManager.NotifyAdmin( thisClassName + ".Save Failed Failed", message );
								}
								
							}
                            else
                            {
                                //update entity.LastUpdated - assuming there has to have been some change in related data
                                new EntityManager().UpdateModifiedDate( entity.RowId, ref status );
                            }
                            if ( isValid )
							{
								if ( !UpdateParts( entity, ref status ) )
									isValid = false;

								SiteActivity sa = new SiteActivity()
								{
									ActivityType = "AssessmentProfile",
									Activity = "Import",
									Event = "Update",
									Comment = string.Format( "Assessment was updated by the import. Name: {0}, SWP: {1}", entity.Name, entity.SubjectWebpage ),
									ActivityObjectId = entity.Id
								};
								new ActivityManager().SiteActivityAdd( sa );
							}
						}
						else
						{
							status.AddError( "Error - update failed, as record was not found." );
						}
					}
					else
					{
						//add
						int newId = Add( entity, ref status );
						if ( newId == 0 || status.HasErrors )
							isValid = false;
					}
				}
			}
			catch ( System.Data.Entity.Validation.DbEntityValidationException dbex )
			{
				string message = HandleDBValidationError( dbex, thisClassName + string.Format( ".Save. id: {0}, Name: {1}", entity.Id, entity.Name ), "Assessment" );
				status.AddError( thisClassName + ".Save(). Error - the save was not successful. " + message );
			}
			catch ( Exception ex )
			{
				string message = FormatExceptions( ex );
				LoggingHelper.LogError( ex, thisClassName + string.Format( ".Save. id: {0}, Name: {1}", entity.Id, entity.Name ) );
				status.AddError( thisClassName + ".Save(). Error - the save was not successful. " + message );
				isValid = false;
			}


            return isValid;
        }

        /// <summary>
        /// add a Assessment
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="status"></param>
        /// <returns></returns>
        private int Add( ThisEntity entity, ref SaveStatus status )
        {
            DBEntity efEntity = new DBEntity();
            using ( var context = new EntityContext() )
            {
                try
                {

					MapToDB( entity, efEntity );

                    if ( IsValidGuid( entity.RowId ) )
                        efEntity.RowId = entity.RowId;
                    else
                        efEntity.RowId = Guid.NewGuid();
					efEntity.EntityStateId = 3;
					efEntity.Created = System.DateTime.Now;
                    efEntity.LastUpdated = System.DateTime.Now;

                    context.Assessment.Add( efEntity );

                    // submit the change to database
                    int count = context.SaveChanges();
                    if ( count > 0 )
                    {
                        entity.Id = efEntity.Id;
                        entity.RowId = efEntity.RowId;
                        //add log entry
                        SiteActivity sa = new SiteActivity()
                        {
                            ActivityType = "AssessmentProfile",
                            Activity = "Import",
                            Event = "Add",
                            Comment = string.Format( "Full Assessment was added by the import. Name: {0}, SWP: {1}", entity.Name, entity.SubjectWebpage ),
                            ActivityObjectId = entity.Id
                        };
                        new ActivityManager().SiteActivityAdd( sa );

                        if ( UpdateParts( entity, ref status ) == false )
                        {
                        }

                        return efEntity.Id;
                    }
                    else
                    {
                        //?no info on error

                        string message = thisClassName + string.Format( ". Add Failed", "Attempted to add a Assessment. The process appeared to not work, but was not an exception, so we have no message, or no clue. Assessment: {0}, ctid: {1}", entity.Name, entity.CTID );
                        status.AddError( thisClassName + ". Error - the add was not successful. " + message );
                        EmailManager.NotifyAdmin( "AssessmentManager. Add Failed", message );
                    }
                }
                catch ( System.Data.Entity.Validation.DbEntityValidationException dbex )
                {
                    string message = HandleDBValidationError( dbex, thisClassName + ".Add() ", "Assessment" );
                    status.AddError( thisClassName + ".Add(). Error - the save was not successful. " + message );

                    LoggingHelper.LogError( message, true );
                }
                catch ( Exception ex )
                {
                    string message = FormatExceptions( ex );
                    LoggingHelper.LogError( ex, thisClassName + string.Format( ".Add(), Name: {0}\r\n", efEntity.Name ) );
                    status.AddError( thisClassName + ".Add(). Error - the save was not successful. \r\n" + message );
                }
            }

			return efEntity.Id;
		}
		public int AddBaseReference( ThisEntity entity, ref SaveStatus status )
		{
			DBEntity efEntity = new DBEntity();
			try
			{
				using ( var context = new EntityContext() )
				{
					if ( entity == null ||
						( string.IsNullOrWhiteSpace( entity.Name ) ||
						string.IsNullOrWhiteSpace( entity.SubjectWebpage )
						) )
					{
						status.AddError( thisClassName + ". AddBaseReference() The assessment is incomplete" );
						return 0;
					}

					//only add DB required properties
					//NOTE - an entity will be created via trigger
					efEntity.EntityStateId = 2;
					efEntity.Name = entity.Name;
					efEntity.Description = entity.Description;
					efEntity.SubjectWebpage = entity.SubjectWebpage;
					if ( IsValidGuid( entity.RowId ) )
						efEntity.RowId = entity.RowId;
					else
						efEntity.RowId = Guid.NewGuid();
                    //set to return, just in case
                    entity.RowId = efEntity.RowId;
                    efEntity.Created = System.DateTime.Now;
					efEntity.LastUpdated = System.DateTime.Now;

					context.Assessment.Add( efEntity );
					int count = context.SaveChanges();
					if ( count > 0 )
					{
						entity.Id = efEntity.Id;
						return efEntity.Id;
					}

					status.AddError( thisClassName + ". AddBaseReference() Error - the save was not successful, but no message provided. " );
				}
			}

			catch ( Exception ex )
			{
				string message = FormatExceptions( ex );
				LoggingHelper.LogError( ex, thisClassName + string.Format( ".AddBaseReference. Name:  {0}, SubjectWebpage: {1}", entity.Name, entity.SubjectWebpage ) );
				status.AddError( thisClassName + ". AddBaseReference() Error - the save was not successful. " + message );

			}
			return 0;
		}
		public int AddPendingRecord( Guid entityUid, string ctid, string registryAtId, ref string status )
		{
			DBEntity efEntity = new DBEntity();
			try
			{
				using ( var context = new EntityContext() )
				{
					if ( !IsValidGuid( entityUid ) )
					{
						status = thisClassName + " - A valid GUID must be provided to create a pending entity";
						return 0;
					}
					//quick check to ensure not existing
					ThisEntity entity = GetByCtid( ctid );
					if ( entity != null && entity.Id > 0 )
						return entity.Id;

					//only add DB required properties
					//NOTE - an entity will be created via trigger
					efEntity.Name = "Placeholder until full document is downloaded";
					efEntity.Description = "Placeholder until full document is downloaded";
					efEntity.EntityStateId = 1;
					efEntity.RowId = entityUid;
					//watch that Ctid can be  updated if not provided now!!
					efEntity.CTID = ctid;
					efEntity.SubjectWebpage = registryAtId;

					efEntity.Created = System.DateTime.Now;
					efEntity.LastUpdated = System.DateTime.Now;

					context.Assessment.Add( efEntity );
					int count = context.SaveChanges();
                    if ( count > 0 )
                    {
                        SiteActivity sa = new SiteActivity()
                        {
                            ActivityType = "AssessmentProfile",
                            Activity = "Import",
                            Event = "Add Pending Assessment",
                            Comment = string.Format( "Pending Assessment was added by the import. ctid: {0}, registryAtId: {1}", ctid, registryAtId ),
                            ActivityObjectId = efEntity.Id
                        };
                        new ActivityManager().SiteActivityAdd( sa );
                        return efEntity.Id;
                    }

                    status = thisClassName + ".AddPendingRecord. Error - the save was not successful, but no message provided. ";
				}
			}

			catch ( Exception ex )
			{
				string message = FormatExceptions( ex );
				LoggingHelper.LogError( ex, thisClassName + string.Format( ".AddPendingRecord. entityUid:  {0}, ctid: {1}", entityUid, ctid ) );
				status = thisClassName + ".AddPendingRecord. Error - the save was not successful. " + message;

			}
			return 0;
		}
		public bool ValidateProfile( ThisEntity profile, ref SaveStatus status )
		{
			status.HasSectionErrors = false;

            if ( string.IsNullOrWhiteSpace( profile.Name ) )
            {
                status.AddError( "An Assessment name must be entered" );
            }
            if ( string.IsNullOrWhiteSpace( profile.Description ) )
            {
                status.AddWarning( "An Assessment Description must be entered" );
            }
            if ( !IsValidGuid( profile.OwningAgentUid ) )
            {
                status.AddWarning( "An owning organization must be selected" );
            }
            if ( !string.IsNullOrWhiteSpace( profile.DateEffective ) && !IsValidDate( profile.DateEffective ) )
            {
                status.AddWarning( "Please enter a valid effective date" );
            }

            if ( string.IsNullOrWhiteSpace( profile.SubjectWebpage ) )
                status.AddWarning( "Error - A Subject Webpage name must be entered" );

            else if ( !IsUrlValid( profile.SubjectWebpage, ref commonStatusMessage ) )
            {
                status.AddWarning( "The Assessment Subject Webpage is invalid. " + commonStatusMessage );
            }

            if ( !IsUrlValid( profile.AvailableOnlineAt, ref commonStatusMessage ) )
            {
                status.AddError( "The Available Online At Url is invalid. " + commonStatusMessage );
            }

            if ( !IsUrlValid( profile.AvailabilityListing, ref commonStatusMessage ) )
            {
                status.AddWarning( "The Availability Listing Url is invalid. " + commonStatusMessage );
            }

            if ( !IsUrlValid( profile.ExternalResearch, ref commonStatusMessage ) )
                status.AddWarning( "The External Research Url is invalid. " + commonStatusMessage );
            if ( !IsUrlValid( profile.ProcessStandards, ref commonStatusMessage ) )
                status.AddWarning( "The Process Standards Url is invalid. " + commonStatusMessage );
            if ( !IsUrlValid( profile.ScoringMethodExample, ref commonStatusMessage ) )
                status.AddWarning( "The Scoring Method Example Url is invalid. " + commonStatusMessage );
            if ( !IsUrlValid( profile.AssessmentExample, ref commonStatusMessage ) )
                status.AddWarning( "The Assessment Example Url is invalid. " + commonStatusMessage );


            //if ( profile.CreditHourValue < 0 || profile.CreditHourValue > 10000 )
            //    status.AddWarning( "Error: invalid value for Credit Hour Value. Must be a reasonable decimal value greater than zero." );

            if ( profile.CreditUnitValue < 0 || profile.CreditUnitValue > 1000 )
                status.AddWarning( "Error: invalid value for Credit Unit Value. Must be a reasonable decimal value greater than zero." );


            //can only have credit hours properties, or credit unit properties, not both
            bool hasCreditHourData = false;
            bool hasCreditUnitData = false;
            //if ( profile.CreditHourValue > 0 || ( profile.CreditHourType ?? "" ).Length > 0 )
            //    hasCreditHourData = true;
            if ( profile.CreditUnitTypeId > 0
                || ( profile.CreditUnitTypeDescription ?? "" ).Length > 0
                || profile.CreditUnitValue > 0 )
                hasCreditUnitData = true;

            if ( hasCreditHourData && hasCreditUnitData )
                status.AddWarning( "Error: Data can be entered for Credit Hour related properties or Credit Unit related properties, but not for both." );


            return !status.HasSectionErrors;
        }


        /// <summary>
        /// Delete an Assessment, and related Entity
        /// </summary>
        /// <param name="Id"></param>
        /// <param name="statusMessage"></param>
        /// <returns></returns>
        //public bool Delete( int Id, ref string statusMessage )
        //{
        //    bool isValid = false;
        //    if ( Id == 0 )
        //    {
        //        statusMessage = "Error - missing an identifier for the Assessment";
        //        return false;
        //    }
        //    using ( var context = new EntityContext() )
        //    {
        //        try
        //        {
        //            context.Configuration.LazyLoadingEnabled = false;
        //            DBEntity efEntity = context.Assessment
        //                        .SingleOrDefault( s => s.Id == Id );

        //            if ( efEntity != null && efEntity.Id > 0 )
        //            {
        //                Guid rowId = efEntity.RowId;

        //                //need to remove from Entity.
        //                //could use a pre-delete trigger?
        //                //what about roles

        //                context.Assessment.Remove( efEntity );
        //                int count = context.SaveChanges();
        //                if ( count > 0 )
        //                {
        //                    isValid = true;
        //                    //add pending request 
        //                    List<String> messages = new List<string>();
        //                    new SearchPendingReindexManager().AddDeleteRequest( CodesManager.ENTITY_TYPE_ASSESSMENT_PROFILE, efEntity.Id, ref messages );
        //                }
        //            }
        //            else
        //            {
        //                statusMessage = "Error - delete failed, as record was not found.";
        //            }
        //        }
        //        catch ( Exception ex )
        //        {
        //            LoggingHelper.LogError( ex, thisClassName + ".Assessment_Delete()" );

        //            if ( ex.InnerException != null && ex.InnerException.Message != null )
        //            {
        //                statusMessage = ex.InnerException.Message;

        //                if ( ex.InnerException.InnerException != null && ex.InnerException.InnerException.Message != null )
        //                    statusMessage = ex.InnerException.InnerException.Message;
        //            }
        //            if ( statusMessage.ToLower().IndexOf( "the delete statement conflicted with the reference constraint" ) > -1 )
        //            {
        //                statusMessage = "Error: this assessment cannot be deleted as it is being referenced by other items, such as roles or credentials. These associations must be removed before this assessment can be deleted.";
        //            }
        //        }
        //    }
        //    return isValid;
        //}

		public bool Delete( string envelopeId, string ctid, ref string statusMessage )
		{
			bool isValid = true;
            if ( ( string.IsNullOrWhiteSpace( envelopeId ) || !IsValidGuid( envelopeId ) )
                && string.IsNullOrWhiteSpace( ctid ) )
            {
                statusMessage = thisClassName + ".Delete() Error - a valid envelope identifier must be provided - OR  valid CTID";
                return false;
            }
            if ( string.IsNullOrWhiteSpace( ctid ) )
                ctid = "SKIP ME";
			int orgId = 0;
			Guid orgUid = new Guid();
			using ( var context = new EntityContext() )
			{
				try
				{
					context.Configuration.LazyLoadingEnabled = false;
					DBEntity efEntity = context.Assessment
                                .FirstOrDefault( s => s.CredentialRegistryId == envelopeId
                                || ( s.CTID == ctid )
                                );

                    if ( efEntity != null && efEntity.Id > 0 )
					{
						Guid rowId = efEntity.RowId;
						if ( IsValidGuid( efEntity.OwningAgentUid ) )
						{
							Organization org = OrganizationManager.GetBasics( ( Guid )efEntity.OwningAgentUid );
							orgId = org.Id;
							orgUid = org.RowId;
						}
						//need to remove from Entity.
						//-using before delete trigger - verify won't have RI issues
						string msg = string.Format( " Assessment. Id: {0}, Name: {1}, Ctid: {2}, EnvelopeId: {3}", efEntity.Id, efEntity.Name, efEntity.CTID, envelopeId );
                        //18-04-05 mparsons - change to set inactive, and notify - seems to have been some incorrect deletes
                        //context.Assessment.Remove( efEntity );
                        efEntity.EntityStateId = 0;
                        efEntity.LastUpdated = System.DateTime.Now;
                        int count = context.SaveChanges();
						if ( count > 0 )
						{
                            new ActivityManager().SiteActivityAdd( new SiteActivity()
                            {
                                ActivityType = "AssessmentProfile",
                                Activity = "Import",
                                Event = "Delete",
								Comment = msg,
								ActivityObjectId = efEntity.Id
							} );
                            isValid = true;
                            //add pending request 
                            List<String> messages = new List<string>();
                            new SearchPendingReindexManager().AddDeleteRequest( CodesManager.ENTITY_TYPE_ASSESSMENT_PROFILE, efEntity.Id, ref messages );
							//mark owning org for updates (actually should be covered by ReindexAgentForDeletedArtifact
							new SearchPendingReindexManager().Add( CodesManager.ENTITY_TYPE_ORGANIZATION, orgId, 1, ref messages );

							//delete all relationships
							workIT.Models.SaveStatus status = new SaveStatus();
							Entity_AgentRelationshipManager earmgr = new Entity_AgentRelationshipManager();
							earmgr.DeleteAll( rowId, ref status );
							//also check for any relationships
							//There could be other orgs from relationships to be reindexed as well!

							//also check for any relationships
							new Entity_AgentRelationshipManager().ReindexAgentForDeletedArtifact( orgUid );
						}
					}
					else
					{
						statusMessage = thisClassName + ".Delete() Warning No action taken, as the record was not found.";
					}
				}
				catch ( Exception ex )
				{
					LoggingHelper.LogError( ex, thisClassName + ".Delete(envelopeId)" );
					isValid = false;
					statusMessage = FormatExceptions( ex );
					if ( statusMessage.ToLower().IndexOf( "the delete statement conflicted with the reference constraint" ) > -1 )
					{
						statusMessage = "Error: this assessment cannot be deleted as it is being referenced by other items, such as roles or credentials. These associations must be removed before this assessment can be deleted.";
					}
				}
			}
			return isValid;
		}

		#region Assessment properties ===================
		public bool UpdateParts( ThisEntity entity, ref SaveStatus status )
        {
            bool isAllValid = true;
			Entity relatedEntity = EntityManager.GetEntity( entity.RowId );
			if ( relatedEntity == null || relatedEntity.Id == 0 )
			{
				status.AddError( "Error - the related Entity was not found." );
				return false;
			}

            if ( UpdateProperties( entity, relatedEntity, ref status ) == false )
            {
                isAllValid = false;
            }

            Entity_ReferenceFrameworkManager erfm = new Entity_ReferenceFrameworkManager();
            erfm.DeleteAll( relatedEntity, ref status );

			if ( erfm.SaveList( relatedEntity.Id, CodesManager.PROPERTY_CATEGORY_SOC, entity.Occupations, ref status ) == false )
				isAllValid = false;
			if ( erfm.SaveList( relatedEntity.Id, CodesManager.PROPERTY_CATEGORY_NAICS, entity.Industries, ref status ) == false )
				isAllValid = false;

			//TODO - handle Naics if provided separately
			if ( erfm.NaicsSaveList( relatedEntity.Id, CodesManager.PROPERTY_CATEGORY_NAICS, entity.Naics, ref status ) == false )
				isAllValid = false;

			if ( erfm.SaveList( relatedEntity.Id, CodesManager.PROPERTY_CATEGORY_CIP, entity.InstructionalProgramTypes, ref status ) == false )
				isAllValid = false;

            Entity_ReferenceManager erm = new Entity_ReferenceManager();
            erm.DeleteAll( relatedEntity, ref status );
            if ( erm.Add( entity.Subject, entity.RowId, CodesManager.ENTITY_TYPE_ASSESSMENT_PROFILE, ref status, CodesManager.PROPERTY_CATEGORY_SUBJECT, false ) == false )
                isAllValid = false;

            if ( erm.Add( entity.Keyword, entity.RowId, CodesManager.ENTITY_TYPE_ASSESSMENT_PROFILE, ref status, CodesManager.PROPERTY_CATEGORY_KEYWORD, false ) == false )
                isAllValid = false;

            erm.AddLanguages( entity.InLanguageCodeList, entity.RowId, CodesManager.ENTITY_TYPE_CREDENTIAL, ref status, CodesManager.PROPERTY_CATEGORY_LANGUAGE );

            AddProfiles( entity, relatedEntity, ref status );


			UpdateAssertedBys( entity, ref status );

			UpdateAssertedIns( entity, ref status );

			return isAllValid;
        }

        public bool UpdateProperties( ThisEntity entity, Entity relatedEntity, ref SaveStatus status )
        {
            bool isAllValid = true;
            EntityPropertyManager mgr = new EntityPropertyManager();
            //first clear all propertiesd
            mgr.DeleteAll( relatedEntity, ref status );
            Entity_ReferenceManager erm = new Entity_ReferenceManager();

            if ( mgr.AddProperties( entity.AssessmentMethodType, entity.RowId, CodesManager.ENTITY_TYPE_ASSESSMENT_PROFILE, CodesManager.PROPERTY_CATEGORY_Assessment_Method_Type, false, ref status ) == false )
                isAllValid = false;

            if ( mgr.AddProperties( entity.AssessmentUseType, entity.RowId, CodesManager.ENTITY_TYPE_ASSESSMENT_PROFILE, CodesManager.PROPERTY_CATEGORY_ASSESSMENT_USE_TYPE, false, ref status ) == false )
                isAllValid = false;

            if ( mgr.AddProperties( entity.DeliveryType, entity.RowId, CodesManager.ENTITY_TYPE_ASSESSMENT_PROFILE, CodesManager.PROPERTY_CATEGORY_DELIVERY_TYPE, false, ref status ) == false )
                isAllValid = false;

            if ( mgr.AddProperties( entity.ScoringMethodType, entity.RowId, CodesManager.ENTITY_TYPE_ASSESSMENT_PROFILE, CodesManager.PROPERTY_CATEGORY_Scoring_Method, false, ref status ) == false )
                isAllValid = false;

			if ( mgr.AddProperties( entity.AudienceLevelType, entity.RowId, CodesManager.ENTITY_TYPE_ASSESSMENT_PROFILE, CodesManager.PROPERTY_CATEGORY_AUDIENCE_LEVEL, false, ref status ) == false )
				isAllValid = false;

			if ( mgr.AddProperties( entity.AudienceType, entity.RowId, CodesManager.ENTITY_TYPE_ASSESSMENT_PROFILE, CodesManager.PROPERTY_CATEGORY_AUDIENCE_TYPE, false, ref status ) == false )
                isAllValid = false;

            return isAllValid;
        }

		public void AddProfiles( ThisEntity entity, Entity relatedEntity, ref SaveStatus status )
		{
			//DurationProfile
			DurationProfileManager dpm = new Factories.DurationProfileManager();
			dpm.SaveList( entity.EstimatedDuration, entity.RowId, ref status );

			//VersionIdentifier
			new Entity_IdentifierValueManager().SaveList( entity.VersionIdentifierList, entity.RowId, Entity_IdentifierValueManager.ASSESSMENT_VersionIdentifier, ref status );

			//CostProfile
			CostProfileManager cpm = new Factories.CostProfileManager();
			cpm.SaveList( entity.EstimatedCost, entity.RowId, ref status );
			try
			{
				//ConditionProfile =======================================
				Entity_ConditionProfileManager emanager = new Entity_ConditionProfileManager();
                emanager.DeleteAll( relatedEntity, ref status );

                emanager.SaveList( entity.Requires, Entity_ConditionProfileManager.ConnectionProfileType_Requirement, entity.RowId, ref status );
				emanager.SaveList( entity.Recommends, Entity_ConditionProfileManager.ConnectionProfileType_Recommendation, entity.RowId, ref status );
				emanager.SaveList( entity.Corequisite, Entity_ConditionProfileManager.ConnectionProfileType_Corequisite, entity.RowId, ref status );
				emanager.SaveList( entity.EntryCondition, Entity_ConditionProfileManager.ConnectionProfileType_EntryCondition, entity.RowId, ref status );

				//Connections
				emanager.SaveList( entity.AdvancedStandingFor, Entity_ConditionProfileManager.ConnectionProfileType_AdvancedStandingFor, entity.RowId, ref status, 3 );
				emanager.SaveList( entity.AdvancedStandingFrom, Entity_ConditionProfileManager.ConnectionProfileType_AdvancedStandingFrom, entity.RowId, ref status, 3 );
				emanager.SaveList( entity.IsPreparationFor, Entity_ConditionProfileManager.ConnectionProfileType_PreparationFor, entity.RowId, ref status, 3 );
				emanager.SaveList( entity.PreparationFrom, Entity_ConditionProfileManager.ConnectionProfileType_PreparationFrom, entity.RowId, ref status, 3 );
				emanager.SaveList( entity.IsRequiredFor, Entity_ConditionProfileManager.ConnectionProfileType_NextIsRequiredFor, entity.RowId, ref status, 3 );
				emanager.SaveList( entity.IsRecommendedFor, Entity_ConditionProfileManager.ConnectionProfileType_NextIsRecommendedFor, entity.RowId, ref status, 3 );
			}
			catch ( Exception ex )
			{
				string message = FormatExceptions( ex );
				LoggingHelper.LogError( ex, thisClassName + string.Format( ".AddProfiles() - ConditionProfiles. id: {0}", entity.Id ) );
				status.AddWarning( thisClassName + ".AddProfiles(). Exceptions encountered handling ConditionProfiles. " + message );
			}

			//ProcessProfile =====================================
			Entity_ProcessProfileManager ppm = new Factories.Entity_ProcessProfileManager();
            ppm.DeleteAll( relatedEntity, ref status );

            ppm.SaveList( entity.AdministrationProcess, Entity_ProcessProfileManager.ADMIN_PROCESS_TYPE, entity.RowId, ref status );
			ppm.SaveList( entity.DevelopmentProcess, Entity_ProcessProfileManager.DEV_PROCESS_TYPE, entity.RowId, ref status );
			ppm.SaveList( entity.MaintenanceProcess, Entity_ProcessProfileManager.MTCE_PROCESS_TYPE, entity.RowId, ref status );

			//Financial Alignment 
			//Entity_FinancialAlignmentProfileManager fapm = new Factories.Entity_FinancialAlignmentProfileManager();
			//fapm.SaveList( entity.FinancialAssistanceOLD, entity.RowId, ref status );

			new Entity_FinancialAssistanceProfileManager().SaveList( entity.FinancialAssistance, entity.RowId, ref status );

			//competencies
			//no need to always do the delete
			new Entity_CompetencyManager().SaveList( entity.AssessesCompetencies, entity.RowId, ref status );

			//addresses
			new Entity_AddressManager().SaveList( entity.Addresses, entity.RowId, ref 
status );
			//JurisdictionProfile 
			Entity_JurisdictionProfileManager jpm = new Entity_JurisdictionProfileManager();
            //do deletes - NOTE: other jurisdictions are added in: UpdateAssertedIns
            jpm.DeleteAll( relatedEntity, ref status );

            jpm.SaveList( entity.Jurisdiction, entity.RowId, Entity_JurisdictionProfileManager.JURISDICTION_PURPOSE_SCOPE, ref status );


			new Entity_CommonConditionManager().SaveList( entity.ConditionManifestIds, entity.RowId, ref status );
			new Entity_CommonCostManager().SaveList( entity.CostManifestIds, entity.RowId, ref status );
		} //

		public bool UpdateAssertedBys( ThisEntity entity, ref SaveStatus status )
		{
			bool isAllValid = true;
			Entity_AgentRelationshipManager mgr = new Entity_AgentRelationshipManager();
			Entity parent = EntityManager.GetEntity( entity.RowId );
			if ( parent == null || parent.Id == 0 )
			{
				status.AddError( thisClassName + " - Error - the parent entity was not found." );
				return false;
			}

            //do deletes - should this be done here, should be no other prior updates?
            mgr.DeleteAll( parent, ref status );

            mgr.SaveList( parent.Id, Entity_AgentRelationshipManager.ROLE_TYPE_AccreditedBy, entity.AccreditedBy, ref status );
			mgr.SaveList( parent.Id, Entity_AgentRelationshipManager.ROLE_TYPE_ApprovedBy, entity.ApprovedBy, ref status );
			mgr.SaveList( parent.Id, Entity_AgentRelationshipManager.ROLE_TYPE_OFFERED_BY, entity.OfferedBy, ref status );
			mgr.SaveList( parent.Id, Entity_AgentRelationshipManager.ROLE_TYPE_OWNER, entity.OwnedBy, ref status );
			mgr.SaveList( parent.Id, Entity_AgentRelationshipManager.ROLE_TYPE_RecognizedBy, entity.RecognizedBy, ref status );
            mgr.SaveList( parent.Id, Entity_AgentRelationshipManager.ROLE_TYPE_RegulatedBy, entity.RegulatedBy, ref status );
            return isAllValid;
		} //

		public void UpdateAssertedIns( ThisEntity entity, ref SaveStatus status )
		{
			Entity_JurisdictionProfileManager mgr = new Entity_JurisdictionProfileManager();
			Entity parent = EntityManager.GetEntity( entity.RowId );
			if ( parent == null || parent.Id == 0 )
			{
				status.AddError( thisClassName + " - Error - the parent entity was not found." );
				return;
			}
            //note the deleteAll is done in AddProfiles

            mgr.SaveAssertedInList( entity.RowId, Entity_AgentRelationshipManager.ROLE_TYPE_AccreditedBy, entity.AccreditedIn, ref status );
			mgr.SaveAssertedInList( entity.RowId, Entity_AgentRelationshipManager.ROLE_TYPE_ApprovedBy, entity.ApprovedIn, ref status );
			mgr.SaveAssertedInList( entity.RowId, Entity_AgentRelationshipManager.ROLE_TYPE_OFFERED_BY, entity.OfferedIn, ref status );

			mgr.SaveAssertedInList( entity.RowId, Entity_AgentRelationshipManager.ROLE_TYPE_RecognizedBy, entity.RecognizedIn, ref status );
			mgr.SaveAssertedInList( entity.RowId, Entity_AgentRelationshipManager.ROLE_TYPE_RegulatedBy, entity.RegulatedIn, ref status );
			
		} //

		#endregion

		#endregion

		#region == Retrieval =======================
		public static ThisEntity GetByCtid( string ctid )
        {
            ThisEntity entity = new ThisEntity();
            using ( var context = new EntityContext() )
            {
                DBEntity from = context.Assessment
                        .FirstOrDefault( s => s.CTID.ToLower() == ctid.ToLower() );

				if ( from != null && from.Id > 0 )
				{
					entity.RowId = from.RowId;
					entity.Id = from.Id;
					entity.EntityStateId = ( int ) ( from.EntityStateId ?? 1 );
					entity.Name = from.Name;
					entity.Description = from.Description;
					entity.SubjectWebpage = from.SubjectWebpage;
					entity.CTID = from.CTID;
					entity.CredentialRegistryId = from.CredentialRegistryId;
				}
			}

			return entity;
		}
		public static ThisEntity GetBySubjectWebpage( string swp )
		{
			ThisEntity entity = new ThisEntity();
			using ( var context = new EntityContext() )
			{
				context.Configuration.LazyLoadingEnabled = false;
				DBEntity from = context.Assessment
						.FirstOrDefault( s => s.SubjectWebpage.ToLower() == swp.ToLower() );

				if ( from != null && from.Id > 0 )
				{
					entity.RowId = from.RowId;
					entity.Id = from.Id;
					entity.Name = from.Name;
					entity.EntityStateId = ( int ) ( from.EntityStateId ?? 1 );
					entity.Description = from.Description;
					entity.SubjectWebpage = from.SubjectWebpage;

					entity.CTID = from.CTID;
					entity.CredentialRegistryId = from.CredentialRegistryId;
				}
			}
			return entity;
		}
        public static ThisEntity GetByName_SubjectWebpage( string name, string swp )
        {
            ThisEntity entity = new ThisEntity();
            using ( var context = new EntityContext() )
            {
                context.Configuration.LazyLoadingEnabled = false;
                DBEntity from = context.Assessment
                        .FirstOrDefault( s => s.Name.ToLower() == name.ToLower() && s.SubjectWebpage.ToLower() == swp.ToLower() );

                if ( from != null && from.Id > 0 )
                {
                    entity.RowId = from.RowId;
                    entity.Id = from.Id;
                    entity.Name = from.Name;
                    entity.EntityStateId = ( int )( from.EntityStateId ?? 1 );
                    entity.Description = from.Description;
                    entity.SubjectWebpage = from.SubjectWebpage;

                    entity.CTID = from.CTID;
                    entity.CredentialRegistryId = from.CredentialRegistryId;
                }
            }
            return entity;
        }
        public static ThisEntity GetBasic( int id )
		{
			ThisEntity entity = new ThisEntity();
			using ( var context = new EntityContext() )
			{
				DBEntity item = context.Assessment
						.SingleOrDefault( s => s.Id == id );

                if ( item != null && item.Id > 0 )
                {
                    MapFromDB_Basic( item, entity, false );
                }
            }

            return entity;
        }
        public static List<ThisEntity> GetAllForOwningOrg( Guid owningOrgUid, ref int totalRecords, int maxRecords = 100 )
        {
            List<ThisEntity> list = new List<ThisEntity>();
            ThisEntity entity = new ThisEntity();
            using (var context = new EntityContext())
            {
                List<DBEntity> results = context.Assessment
                             .Where( s => s.OwningAgentUid == owningOrgUid )
                             .OrderBy( s => s.Name )
                             .ToList();
                if (results != null && results.Count > 0)
                {
					totalRecords = results.Count();

					foreach (DBEntity item in results)
                    {
                        entity = new ThisEntity();
                        MapFromDB_Basic( item, entity, false );
                        list.Add( entity );
						if ( maxRecords > 0 && list.Count >= maxRecords )
							break;
					}
                }
            }

            return list;
        }
		public static List<ThisEntity> GetAll( ref int totalRecords, int maxRecords = 100 )
		{
			List<ThisEntity> list = new List<ThisEntity>();
			ThisEntity entity = new ThisEntity();
			using ( var context = new EntityContext() )
			{
				List<DBEntity> results = context.Assessment
							 .Where( s => s.EntityStateId > 2 )
							 .OrderBy( s => s.Name )
							 .ToList();
				if ( results != null && results.Count > 0 )
				{
					totalRecords = results.Count();

					foreach ( DBEntity item in results )
					{
						entity = new ThisEntity();
						MapFromDB_Basic( item, entity, false );
						list.Add( entity );
						if ( maxRecords > 0 && list.Count >= maxRecords )
							break;
					}
				}
			}

			return list;
		}
		public static ThisEntity GetDetails( int id )
        {
            ThisEntity entity = new ThisEntity();
            using ( var context = new EntityContext() )
            {
                DBEntity item = context.Assessment
                        .SingleOrDefault( s => s.Id == id );

                if ( item != null && item.Id > 0 )
                {
                    //check for virtual deletes
                    if (item.EntityStateId == 0)
                        return entity;

                    MapFromDB( item, entity,
                            true, //includingProperties
                            true, //includingRoles
                            true );
                }
            }

            return entity;
        }


        public static List<object> Autocomplete( string pFilter, int pageNumber, int pageSize, ref int pTotalRows )
        {
            bool autocomplete = true;
            List<object> results = new List<object>();
            List<string> competencyList = new List<string>();
            //ref competencyList, 
            List<ThisEntity> list = Search( pFilter, "", pageNumber, pageSize, ref pTotalRows, autocomplete );
			bool appendingOrgNameToAutocomplete = UtilityManager.GetAppKeyValue( "appendingOrgNameToAutocomplete", false );
			string prevName = "";
            foreach ( AssessmentProfile item in list )
            {
				//note excluding duplicates may have an impact on selected max terms
				if ( string.IsNullOrWhiteSpace( item.OrganizationName )
	|| !appendingOrgNameToAutocomplete )
				{
					if ( item.Name.ToLower() != prevName )
						results.Add( item.Name );
				}
				else
				{
					results.Add( item.Name + " ('" + item.OrganizationName + "')" );
				}

				prevName = item.Name.ToLower();
            }
            return results;
        }
        /// <summary>
        /// Search for assessments
        /// </summary>
        /// <returns></returns>
        //public static List<ThisEntity> QuickSearch( int userId, string keyword, int pageNumber, int pageSize, ref int pTotalRows )
        //{
        //	List<ThisEntity> list = new List<ThisEntity>();
        //	ThisEntity entity = new ThisEntity();
        //	keyword = string.IsNullOrWhiteSpace( keyword ) ? "" : keyword.Trim();
        //	if ( pageSize == 0 )
        //		pageSize = 500;
        //	int skip = 0;
        //	if ( pageNumber > 1 )
        //		skip = ( pageNumber - 1 ) * pageSize;

        //	using ( var context = new EntityContext() )
        //	{
        //		var Query = from Results in context.Assessment
        //				.Where( s => keyword == "" || s.Name.Contains( keyword ) )
        //				.OrderBy( s => s.Name )
        //				select Results;
        //		pTotalRows = Query.Count();
        //		var results = Query.Skip(skip).Take( pageSize )
        //			.ToList();

        //		//List<DBEntity> results = context.Assessment
        //		//	.Where( s => keyword == "" || s.Name.Contains( keyword ) )
        //		//	.Take( pageSize )
        //		//	.OrderBy( s => s.Name )
        //		//	.ToList();

        //		if ( results != null && results.Count > 0 )
        //		{
        //			foreach ( DBEntity item in results )
        //			{
        //				entity = new ThisEntity();
        //				MapFromDB( item, entity,
        //						false, //includingProperties
        //						false, //includingRoles
        //						false //includeWhereUsed
        //						 );
        //				list.Add( entity );
        //			}

        //			//Other parts
        //		}
        //	}

        //	return list;
        //}

        public static List<ThisEntity> Search( string pFilter, string pOrderBy, int pageNumber, int pageSize, ref int pTotalRows, bool autocomplete = false )
        {
            string connectionString = DBConnectionRO();
            ThisEntity item = new ThisEntity();
            List<ThisEntity> list = new List<ThisEntity>();
            var result = new DataTable();
            string temp = "";
            string org = "";
            int orgId = 0;

            using ( SqlConnection c = new SqlConnection( connectionString ) )
            {
                c.Open();

                if ( string.IsNullOrEmpty( pFilter ) )
                {
                    pFilter = "";
                }

                using ( SqlCommand command = new SqlCommand( "[Assessment_Search]", c ) )
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.Add( new SqlParameter( "@Filter", pFilter ) );
                    command.Parameters.Add( new SqlParameter( "@SortOrder", pOrderBy ) );
                    command.Parameters.Add( new SqlParameter( "@StartPageIndex", pageNumber ) );
                    command.Parameters.Add( new SqlParameter( "@PageSize", pageSize ) );

                    SqlParameter totalRows = new SqlParameter( "@TotalRows", pTotalRows );
                    totalRows.Direction = ParameterDirection.Output;
                    command.Parameters.Add( totalRows );
                    try
                    {
                    using ( SqlDataAdapter adapter = new SqlDataAdapter() )
                    {
                        adapter.SelectCommand = command;
                        adapter.Fill( result );
                    }
                    string rows = command.Parameters[command.Parameters.Count - 1].Value.ToString();
                 
                        pTotalRows = Int32.Parse( rows );
                    }
                    catch (Exception ex)
                    {
                        pTotalRows = 0;
                        LoggingHelper.LogError(ex, thisClassName + string.Format(".Search() - Execute proc, Message: {0} \r\n Filter: {1} \r\n", ex.Message, pFilter));

                        item = new AssessmentProfile();
                        item.Name = "Unexpected error encountered. System administration has been notified. Please try again later. ";
                        item.Description = ex.Message;

                        list.Add(item);
                        return list;
                    }
                }

                foreach ( DataRow dr in result.Rows )
                {
                    item = new ThisEntity();
                    item.Id = GetRowColumn( dr, "Id", 0 );
                    item.Name = GetRowColumn( dr, "Name", "missing" );
                    item.FriendlyName = FormatFriendlyTitle( item.Name );
                    //for autocomplete, only need name
                    if ( autocomplete )
                    {
                        list.Add( item );
                        continue;
                    }

                    item.Description = GetRowColumn( dr, "Description", "" );
                    string rowId = GetRowColumn( dr, "RowId" );
                    item.RowId = new Guid( rowId );

                    item.SubjectWebpage = GetRowColumn( dr, "SubjectWebpage", "" );
                    item.AvailableOnlineAt = GetRowPossibleColumn( dr, "AvailableOnlineAt", "" );

                    item.CodedNotation = GetRowColumn( dr, "IdentificationCode", "" );
                    item.CTID = GetRowPossibleColumn( dr, "CTID", "" );
                    item.CredentialRegistryId = GetRowPossibleColumn( dr, "CredentialRegistryId", "" );

                    item.RequiresCount = GetRowPossibleColumn( dr, "RequiresCount", 0 );
                    item.RecommendsCount = GetRowPossibleColumn( dr, "RecommendsCount", 0 );
                    item.RequiredForCount = GetRowPossibleColumn( dr, "IsRequiredForCount", 0 );
                    item.IsRecommendedForCount = GetRowPossibleColumn( dr, "IsRecommendedForCount", 0 );
                    item.IsAdvancedStandingForCount = GetRowPossibleColumn( dr, "IsAdvancedStandingForCount", 0 );
                    item.AdvancedStandingFromCount = GetRowPossibleColumn( dr, "AdvancedStandingFromCount", 0 );
                    item.PreparationForCount = GetRowPossibleColumn( dr, "IsPreparationForCount", 0 );
                    item.PreparationFromCount = GetRowPossibleColumn( dr, "IsPreparationFromCount", 0 );

                    item.ListTitle = item.Name + " (" + item.OwnerOrganizationName + ")";
                    item.QualityAssurance = Fill_AgentRelationship( dr, "QualityAssurance", CodesManager.PROPERTY_CATEGORY_CREDENTIAL_AGENT_ROLE, false, false, true );

                    org = GetRowPossibleColumn( dr, "Organization", "" );
                    orgId = GetRowPossibleColumn( dr, "OrgId", 0 );
                    if ( orgId > 0 )
                        item.OwningOrganization = new Organization() { Id = orgId, Name = org };

                    temp = GetRowColumn( dr, "DateEffective", "" );
                    if ( IsValidDate( temp ) )
                        item.DateEffective = DateTime.Parse( temp ).ToShortDateString();
                    else
                        item.DateEffective = "";

                    item.Created = GetRowColumn( dr, "Created", System.DateTime.MinValue );
                    item.LastUpdated = GetRowColumn( dr, "LastUpdated", System.DateTime.MinValue );

                    //addressess
                    int addressess = GetRowPossibleColumn( dr, "AvailableAddresses", 0 );
                    if ( addressess > 0 )
                    {
                        item.Addresses = Entity_AddressManager.GetAll( item.RowId );
                    }
                    //not used yet
                    item.CompetenciesCount = GetRowPossibleColumn( dr, "Competencies", 0 );
					if ( item.CompetenciesCount > 0 )
					{
						//handled in search services
						//List<string> competencyList = new List<string>();
						//FillCompetencies( item, ref competencyList );
					}
					list.Add( item );
                }

                return list;

			}
		} //
		
		public static void MapToDB( ThisEntity input, DBEntity output )
		{

            //want output ensure fields input create are not wiped
            if ( output.Id == 0 )
            {
                output.CTID = input.CTID;
            }
            if ( !string.IsNullOrWhiteSpace( input.CredentialRegistryId ) )
                output.CredentialRegistryId = input.CredentialRegistryId;

            output.Id = input.Id;
            output.Name = GetData( input.Name );
            output.Description = GetData( input.Description );
            output.IdentificationCode = GetData( input.CodedNotation );
            output.VersionIdentifier = GetData( input.VersionIdentifier );

            //output.OtherAssessmentType = GetData( input.OtherAssessmentType );

            output.SubjectWebpage = GetUrlData( input.SubjectWebpage );
            output.AvailableOnlineAt = GetUrlData( input.AvailableOnlineAt );
            output.AvailabilityListing = GetUrlData( input.AvailabilityListing );
            output.AssessmentExampleUrl = GetData( input.AssessmentExample );

            if ( IsGuidValid( input.OwningAgentUid ) )
            {
                if ( output.Id > 0 && output.OwningAgentUid != input.OwningAgentUid )
                {
                    if ( IsGuidValid( output.OwningAgentUid ) )
                    {
                        //need output remove the owner role, or could have been others
                        string statusMessage = "";
                        new Entity_AgentRelationshipManager().Delete( output.RowId, output.OwningAgentUid, Entity_AgentRelationshipManager.ROLE_TYPE_OWNER, ref statusMessage );
                    }
                }
                output.OwningAgentUid = input.OwningAgentUid;
                //get for use to add to elastic pending
                input.OwningOrganization = OrganizationManager.GetForSummary( input.OwningAgentUid );
                //input.OwningOrganizationId = org.Id;
            }
            else
            {
                //always have output have an owner
                //output.OwningAgentUid = null;
            }

			//if ( input.InLanguageId > 0 )
			//	output.InLanguageId = input.InLanguageId;
			//else if ( !string.IsNullOrWhiteSpace( input.InLanguage ) )
			//{
			//	output.InLanguageId = CodesManager.GetLanguageId( input.InLanguage );
			//}
			//else
				output.InLanguageId = null;

			//======================================================================
			if ( input.CreditValue.HasData() )
			{
				if ( input.CreditValue.CreditUnitType != null && input.CreditValue.CreditUnitType.HasItems() )
				{
					//get Id if available
					EnumeratedItem item = input.CreditValue.CreditUnitType.GetFirstItem();
					if ( item != null && item.Id > 0 )
						output.CreditUnitTypeId = item.Id;
					else
					{
						//if not get by schema
						CodeItem code = CodesManager.GetPropertyBySchema( "ceterms:CreditUnit", item.SchemaName );
						output.CreditUnitTypeId = code.Id;
					}
				}
				output.CreditUnitValue = input.CreditValue.Value;
				output.CreditUnitMaxValue = input.CreditValue.MaxValue;
				if ( input.CreditValue.MaxValue > 0)
					output.CreditUnitValue = input.CreditValue.MinValue;
				output.CreditUnitTypeDescription = input.CreditValue.Description;
			}
			else if ( UtilityManager.GetAppKeyValue( "usingQuantitiveValue", false ) == false )
			{

				//output.CreditHourType = GetData( input.CreditHourType, null );
				//output.CreditHourValue = SetData( input.CreditHourValue, 0.5M );
				//output.CreditUnitTypeId = SetData( input.CreditUnitTypeId, 1 );
				if ( input.CreditUnitType != null && input.CreditUnitType.HasItems() )
				{
					//get Id if available
					EnumeratedItem item = input.CreditUnitType.GetFirstItem();
					if ( item != null && item.Id > 0 )
						output.CreditUnitTypeId = item.Id;
					else
					{
						//if not get by schema
						CodeItem code = CodesManager.GetPropertyBySchema( "ceterms:CreditUnit", item.SchemaName );
						output.CreditUnitTypeId = code.Id;
					}
				}
				output.CreditUnitTypeDescription = GetData( input.CreditUnitTypeDescription );
				output.CreditUnitValue = SetData( input.CreditUnitValue, 0.5M );
			}
			//========================================================================
            output.DeliveryTypeDescription = input.DeliveryTypeDescription;
            //output.VerificationMethodDescription = input.VerificationMethodDescription;
            output.AssessmentExampleDescription = input.AssessmentExampleDescription;
            output.AssessmentOutput = input.AssessmentOutput;
            output.ExternalResearch = input.ExternalResearch;

            output.HasGroupEvaluation = input.HasGroupEvaluation;
            output.HasGroupParticipation = input.HasGroupParticipation;
            output.IsProctored = input.IsProctored;

            output.ProcessStandards = input.ProcessStandards;
            output.ProcessStandardsDescription = input.ProcessStandardsDescription;

            output.ScoringMethodDescription = input.ScoringMethodDescription;
            output.ScoringMethodExample = input.ScoringMethodExample;
            output.ScoringMethodExampleDescription = input.ScoringMethodExampleDescription;



            if ( IsValidDate( input.DateEffective ) )
                output.DateEffective = DateTime.Parse( input.DateEffective );
            else
                output.DateEffective = null;


            if ( IsValidDate( input.LastUpdated ) )
                output.LastUpdated = input.LastUpdated;
        }

        public static void MapFromDB( DBEntity from, ThisEntity to,
                bool includingProperties,
                bool includingRoles,
                bool includeWhereUsed )
        {
            MapFromDB_Basic( from, to, true );

            to.CredentialRegistryId = from.CredentialRegistryId;

            
            to.AvailabilityListing = from.AvailabilityListing;

            to.AssessmentExample = from.AssessmentExampleUrl;
            to.AssessmentExampleDescription = from.AssessmentExampleDescription;


            if ( IsValidDate( from.DateEffective ) )
                to.DateEffective = ( ( DateTime )from.DateEffective ).ToShortDateString();
            else
                to.DateEffective = "";

            to.CodedNotation = from.IdentificationCode;
            to.AvailableOnlineAt = from.AvailableOnlineAt;

			//multiple languages, now in entity.reference
			to.InLanguageCodeList = Entity_ReferenceManager.GetAll( to.RowId, CodesManager.PROPERTY_CATEGORY_LANGUAGE );
			//=========================================================
			//populate QV
			to.CreditValue = FormatQuantitiveValue( from.CreditUnitTypeId, from.CreditUnitValue, from.CreditUnitMaxValue, from.CreditUnitTypeDescription );
			if ( to.CreditValue.HasData() )
			{
				to.CreditUnitType = to.CreditValue.CreditUnitType;
				to.CreditUnitTypeId = ( from.CreditUnitTypeId ?? 0 );
				to.CreditUnitTypeDescription = to.CreditValue.Description;

				to.CreditUnitValue = to.CreditValue.Value;
				to.CreditUnitMaxValue = to.CreditValue.MaxValue;


			} else
			{
				//check for old
				to.CreditUnitTypeId = ( from.CreditUnitTypeId ?? 0 );
				to.CreditUnitTypeDescription = from.CreditUnitTypeDescription;
				to.CreditUnitValue = from.CreditUnitValue ?? 0M;
				to.CreditUnitMaxValue = from.CreditUnitMaxValue ?? 0M;
				//temp handling of clock hpurs
				//to.CreditHourType = from.CreditHourType ?? "";
				//to.CreditHourValue = ( from.CreditHourValue ?? 0M );
				//if ( to.CreditHourValue > 0 )
				//{
				//	to.CreditUnitValue = to.CreditHourValue;
				//	to.CreditUnitTypeDescription = to.CreditHourType;
				//}
			}

			//=============================

			to.DeliveryTypeDescription = from.DeliveryTypeDescription;
			//to.VerificationMethodDescription = from.VerificationMethodDescription;
			to.AssessmentOutput = from.AssessmentOutput;
            to.ExternalResearch = from.ExternalResearch;
            if ( from.HasGroupEvaluation != null )
                to.HasGroupEvaluation = ( bool )from.HasGroupEvaluation;
            if ( from.HasGroupParticipation != null )
                to.HasGroupParticipation = ( bool )from.HasGroupParticipation;
            if ( from.IsProctored != null )
                to.IsProctored = ( bool )from.IsProctored;

            to.ProcessStandards = from.ProcessStandards;
            to.ProcessStandardsDescription = from.ProcessStandardsDescription;

            to.ScoringMethodDescription = from.ScoringMethodDescription;
            to.ScoringMethodExample = from.ScoringMethodExample;
            to.ScoringMethodExampleDescription = from.ScoringMethodExampleDescription;
            to.AudienceType = EntityPropertyManager.FillEnumeration( to.RowId,CodesManager.PROPERTY_CATEGORY_AUDIENCE_TYPE );
			to.AudienceLevelType = EntityPropertyManager.FillEnumeration( to.RowId, CodesManager.PROPERTY_CATEGORY_AUDIENCE_LEVEL );

			to.Subject = Entity_ReferenceManager.GetAll( to.RowId, CodesManager.PROPERTY_CATEGORY_SUBJECT );

            to.Keyword = Entity_ReferenceManager.GetAll( to.RowId, CodesManager.PROPERTY_CATEGORY_KEYWORD );
            //properties
            try
            {
                if ( includingProperties )
                {
                    to.AssessmentMethodType = EntityPropertyManager.FillEnumeration(to.RowId, CodesManager.PROPERTY_CATEGORY_Assessment_Method_Type);

                    to.AssessmentUseType = EntityPropertyManager.FillEnumeration(to.RowId, CodesManager.PROPERTY_CATEGORY_ASSESSMENT_USE_TYPE);

                    to.DeliveryType = EntityPropertyManager.FillEnumeration(to.RowId, CodesManager.PROPERTY_CATEGORY_DELIVERY_TYPE);

                    to.ScoringMethodType = EntityPropertyManager.FillEnumeration(to.RowId, CodesManager.PROPERTY_CATEGORY_Scoring_Method);

                    to.Addresses = Entity_AddressManager.GetAll(to.RowId);

                    // Begin edits - Need these to populate Credit Unit Type -  NA 3/24/2017
                    if ( to.CreditUnitTypeId > 0 )
                    {
                        to.CreditUnitType = new Enumeration();
                        var match = CodesManager.GetEnumeration("creditUnit").Items.FirstOrDefault(m => m.CodeId == to.CreditUnitTypeId);
                        if ( match != null )
                        {
                            to.CreditUnitType.Items.Add(match);
                        }
                    }

                    //this is in MapFromDB_Basic
                    //to.EstimatedCost = CostProfileManager.GetAll( to.RowId, forEditView );

                }
            }
            catch ( Exception ex )
            {
                LoggingHelper.LogError(ex, thisClassName + string.Format(".MapFromDB_A(), Name: {0} ({1})", to.Name, to.Id));
                to.StatusMessage = FormatExceptions(ex);
            }

            try
            {
                //get competencies
                MapFromDB_Competencies(to);

				to.Occupation = Reference_FrameworksManager.FillEnumeration( to.RowId, CodesManager.PROPERTY_CATEGORY_SOC );

				to.Industry = Reference_FrameworksManager.FillEnumeration( to.RowId, CodesManager.PROPERTY_CATEGORY_NAICS );

				to.InstructionalProgramType = Reference_FrameworksManager.FillEnumeration(to.RowId, CodesManager.PROPERTY_CATEGORY_CIP);

                if ( includingRoles )
                {

                    //get as ennumerations
                    //to.OrganizationRole = Entity_AgentRelationshipManager.AgentEntityRole_GetAll_ToEnumeration(to.RowId, true);
                    to.OrganizationRole = Entity_AssertionManager.GetAllCombinedForTarget( 3, to.Id, to.OwningOrganizationId );
                    //}
                    //to.QualityAssuranceAction =	Entity_QualityAssuranceActionManager.QualityAssuranceActionProfile_GetAll( to.RowId );

                    to.EstimatedDuration = DurationProfileManager.GetAll(to.RowId);

                    to.Jurisdiction = Entity_JurisdictionProfileManager.Jurisdiction_GetAll(to.RowId);
                    to.Region = Entity_JurisdictionProfileManager.Jurisdiction_GetAll(to.RowId, Entity_JurisdictionProfileManager.JURISDICTION_PURPOSE_RESIDENT);
                    to.JurisdictionAssertions = Entity_JurisdictionProfileManager.Jurisdiction_GetAll(to.RowId, Entity_JurisdictionProfileManager.JURISDICTION_PURPOSE_OFFERREDIN);

                }


                //get condition profiles
                List<ConditionProfile> list = new List<ConditionProfile>();

                list = Entity_ConditionProfileManager.GetAll(to.RowId, false);
                if ( list != null && list.Count > 0 )
                {
                    foreach ( ConditionProfile item in list )
                    {
                        if ( item.ConditionSubTypeId == Entity_ConditionProfileManager.ConditionSubType_Assessment )
                        {
                            to.AssessmentConnections.Add(item);
                        }
                        else if ( item.ConnectionProfileTypeId == Entity_ConditionProfileManager.ConnectionProfileType_Requirement )
                            to.Requires.Add(item);
                        else if ( item.ConnectionProfileTypeId == Entity_ConditionProfileManager.ConnectionProfileType_Recommendation )
                            to.Recommends.Add(item);
                        else if ( item.ConnectionProfileTypeId == Entity_ConditionProfileManager.ConnectionProfileType_Corequisite )
                            to.Corequisite.Add(item);
                        else if ( item.ConnectionProfileTypeId == Entity_ConditionProfileManager.ConnectionProfileType_EntryCondition )
                            to.EntryCondition.Add(item);
                        else
                        {
                            EmailManager.NotifyAdmin("Unexpected Condition Profile for assessment", string.Format("AssessmentId: {0}, ConditionProfileTypeId: {1}", to.Id, item.ConnectionProfileTypeId));

                            //add to required, for dev only?
                            if ( IsDevEnv() )
                            {
                                item.ProfileName = ( item.ProfileName ?? "" ) + " unexpected condition type of " + item.ConnectionProfileTypeId.ToString();
                                to.Requires.Add(item);
                            }
                        }
                    }


                }
            }
            catch ( Exception ex )
            {
                LoggingHelper.LogError(ex, thisClassName + string.Format(".MapFromDB_B(), Name: {0} ({1})", to.Name, to.Id));
                to.StatusMessage = FormatExceptions(ex);
            }

            to.CommonConditions = Entity_CommonConditionManager.GetAll( to.RowId );

            to.CommonCosts = Entity_CommonCostManager.GetAll( to.RowId );

			to.FinancialAssistance = Entity_FinancialAssistanceProfileManager.GetAll( to.RowId, false );
			//TODO
			List<ProcessProfile> processes = Entity_ProcessProfileManager.GetAll( to.RowId );
			foreach ( ProcessProfile item in processes )
			{
				if ( item.ProcessTypeId == Entity_ProcessProfileManager.ADMIN_PROCESS_TYPE )
					to.AdministrationProcess.Add( item );
				else if ( item.ProcessTypeId == Entity_ProcessProfileManager.DEV_PROCESS_TYPE )
					to.DevelopmentProcess.Add( item );
				else if ( item.ProcessTypeId == Entity_ProcessProfileManager.MTCE_PROCESS_TYPE )
					to.MaintenanceProcess.Add( item );
				else
				{
					//unexpected
				}
			}

			to.WhereReferenced = new List<string>();
            if ( includeWhereUsed )
            {
			}
			if ( from.Entity_Assessment != null && from.Entity_Assessment.Count > 0 )
			{
				//TODO
				foreach ( EM.Entity_Assessment item in from.Entity_Assessment )
				{
					//to.WhereReferenced.Add( string.Format( "EntityUid: {0}, Type: {1}", item.Entity.EntityUid, item.Entity.Codes_EntityTypes.Title ) );

					//only parent for now
					if ( item.Entity.EntityTypeId == CodesManager.ENTITY_TYPE_CONNECTION_PROFILE )
					{
						ConditionProfile cp = CondProfileMgr.GetAs_IsPartOf( item.Entity.EntityUid );
						to.IsPartOfConditionProfile.Add( cp );
						//need to check cond prof for parent of credential
						//will need to ensure no dups, or realistically, don't do the direct credential check
						if ( cp.ParentCredential != null && cp.ParentCredential.Id > 0 )
						{
							if ( cp.ParentCredential.EntityStateId > 1 )
							{
								AddCredentialReference( cp.ParentCredential.Id, to );
								//to.IsPartOfCredential.Add( cp.ParentCredential );
							}
						}

					}
					else if ( item.Entity.EntityTypeId == CodesManager.ENTITY_TYPE_LEARNING_OPP_PROFILE )
					{
						var lo = LearningOpportunityManager.GetAs_IsPartOf( item.Entity.EntityUid );
						if ( lo.EntityStateId > 1 )
							to.IsPartOfLearningOpp.Add( lo );
					}
					else if ( item.Entity.EntityTypeId == CodesManager.ENTITY_TYPE_CREDENTIAL )
					{
						//this is not possible in the finder
						//AddCredentialReference( ( int ) item.Entity.EntityBaseId, to );

					}
				}
			}


		}
		private static void AddCredentialReference( int credentialId, ThisEntity to )
        {
            Credential exists = to.IsPartOfCredential.SingleOrDefault( s => s.Id == credentialId );
            if ( exists == null || exists.Id == 0 )
                to.IsPartOfCredential.Add( CredentialManager.GetBasic( ( int )credentialId ) );
        } //

		public static void MapFromDB_Basic( DBEntity from, ThisEntity to, bool includingCosts )
		{
			to.Id = from.Id;
			to.RowId = from.RowId;
			to.EntityStateId = ( int ) ( from.EntityStateId ?? 1 );
			if ( IsGuidValid( from.OwningAgentUid ) )
			{
				to.OwningAgentUid = ( Guid ) from.OwningAgentUid;
				to.OwningOrganization = OrganizationManager.GetForSummary( to.OwningAgentUid );

                //get roles
                OrganizationRoleProfile orp = Entity_AgentRelationshipManager.AgentEntityRole_GetAsEnumerationFromCSV( to.RowId, to.OwningAgentUid );
                to.OwnerRoles = orp.AgentRole;
            }
            to.Name = from.Name;
            to.Description = from.Description == null ? "" : from.Description;
            to.CTID = from.CTID;

            to.SubjectWebpage = from.SubjectWebpage;
            if (IsValidDate( from.Created ))
                to.Created = ( DateTime ) from.Created;
            if (IsValidDate( from.LastUpdated ))
                to.LastUpdated = ( DateTime ) from.LastUpdated;

			to.AssessmentExample = from.AssessmentExampleUrl;
			to.AvailabilityListing = from.AvailabilityListing;
			to.AvailableOnlineAt = from.AvailableOnlineAt;
			to.ExternalResearch = from.ExternalResearch;

            if (string.IsNullOrWhiteSpace( to.CTID ) || to.EntityStateId < 3)
            {
                to.IsReferenceVersion = true;
                return;
            }
			//=====
			var relatedEntity = EntityManager.GetEntity( to.RowId, false );
			if ( relatedEntity != null && relatedEntity.Id > 0 )
				to.EntityLastUpdated = relatedEntity.LastUpdated;
			try
            {
                //**TODO VersionIdentifier - need to change to a list of IdentifierValue
                to.VersionIdentifier = from.VersionIdentifier;
                //assumes only one identifier type per class
                to.VersionIdentifierList = Entity_IdentifierValueManager.GetAll(to.RowId, Entity_IdentifierValueManager.ASSESSMENT_VersionIdentifier);

                //costs may be required for the list view, when called by the credential editor
                //make configurable
                if ( includingCosts )
                {
                    to.EstimatedCost = CostProfileManager.GetAll(to.RowId);
                    to.CommonCosts = Entity_CommonCostManager.GetAll(to.RowId);

                    //Include currencies to fix null errors in various places (detail, compare, publishing) - NA 3/17/2017
                    var currencies = CodesManager.GetCurrencies();
                    //Include cost types to fix other null errors - NA 3/31/2017
                    var costTypes = CodesManager.GetEnumeration(CodesManager.PROPERTY_CATEGORY_CREDENTIAL_ATTAINMENT_COST);
                    foreach ( var cost in to.EstimatedCost )
                    {
                        cost.CurrencyTypes = currencies;

                        foreach ( var costItem in cost.Items )
                        {
                            costItem.DirectCostType.Items.Add(costTypes.Items.FirstOrDefault(m => m.CodeId == costItem.CostTypeId));
                        }
                    }
                    //End edits - NA 3/31/2017
                }

                //Need this for the detail page, since we now show durations by profile name - NA 4/13/2017
                to.EstimatedDuration = DurationProfileManager.GetAll(to.RowId);
                to.FinancialAssistanceOLD = Entity_FinancialAlignmentProfileManager.GetAll(to.RowId);
            }
            catch ( Exception ex )
            {
                LoggingHelper.LogError(ex, thisClassName + string.Format(".MapFromDB_Basic(), Name: {0} ({1})", to.Name, to.Id));
                to.StatusMessage = FormatExceptions(ex);
            }



        } //

        public static void MapFromDB_Competencies( ThisEntity to )
        {

            var frameworksList = new Dictionary<string, RegistryImport>();
            //AssessesCompetencies is only used by import
            //to.AssessesCompetencies = Entity_CompetencyManager.GetAllAs_CredentialAlignmentObjectProfile( to.RowId, ref frameworksList);
            //to.FrameworkPayloads = frameworksList;
            //if ( to.AssessesCompetencies.Count > 0 )
            //    to.HasCompetencies = true;

            to.AssessesCompetenciesFrameworks = new List<CredentialAlignmentObjectFrameworkProfile>();
			to.AssessesCompetenciesFrameworks = Entity_CompetencyManager.GetAllAs_CAOFramework(  to.RowId, ref frameworksList);
            if ( to.AssessesCompetenciesFrameworks.Count > 0 )
            {
                to.HasCompetencies = true;
                to.FrameworkPayloads = frameworksList;
            }
		}

        /// <summary>
        /// Fill actual competencies for entity
        /// </summary>
        /// <param name="item"></param>
        /// <param name="competencyList">Contains any competencies from filters</param>
        //private static void FillCompetencies(ThisEntity item, ref List<string> competencyList)
        //{
        //    var frameworksList = new Dictionary<string, RegistryImport>();
        //    item.AssessesCompetenciesFrameworks = new List<CredentialAlignmentObjectFrameworkProfile>();
        //    //return;
        //    //TODO - not using frameworks, the latter would have flattened items to CredentialAlignmentObjectProfile, which we do have
        //    if ( competencyList.Count == 0 )
        //        item.AssessesCompetenciesFrameworks = Entity_CompetencyManager.GetAllAs_CAOFramework(item.RowId, ref frameworksList);
        //    else
        //    {

        //        List<CredentialAlignmentObjectFrameworkProfile> all = Entity_CompetencyManager.GetAllAs_CAOFramework(item.RowId, ref frameworksList);
        //        foreach ( CredentialAlignmentObjectFrameworkProfile next in all )
        //        {
        //            //just do desc for now
        //            string orig = ( next.Description ?? "" );
        //            foreach ( string filter in competencyList )
        //            {
        //                //not ideal, as would be an exact match
        //                orig = orig.Replace(filter, string.Format("<span class='highlight'>{0}<\\span>", filter));
        //            }
        //            if ( orig != ( next.Description ?? "" ) )
        //            {
        //                next.Description = orig;
        //                item.AssessesCompetenciesFrameworks.Add(next);
        //            }
        //        }
        //    }
        //}
        #endregion

    }
}
