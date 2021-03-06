USE [credFinder]
GO
--USE staging_credFinder
--GO
/****** Object:  StoredProcedure [dbo].[LearningOpportunity.ElasticSearch]    Script Date: 1/22/2018 4:26:03 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

/*
use credFinder
GO

SELECT [Id]
      ,[Name]
      ,[Description]
      ,[Address1]      ,[Address2]
      ,[City]      ,[Region]      ,[PostalCode]      ,[Country]
      ,[Email]
      ,[MainPhoneNumber]
      ,[URL]
      ,[UniqueURI]      ,[ImageURL]
      ,[RowId]
      ,[CredentialCount]
      ,[IsAQAOrganization]
     -- ,[Created]  ,[LastUpdated]    

  FROM [dbo].[Organization_Summary]
GO

--=====================================================

DECLARE @RC int,@SortOrder varchar(100),@Filter varchar(5000)
DECLARE @StartPageIndex int, @PageSize int, @TotalRows int
--

set @SortOrder = 'newest'
set @SortOrder = 'cost_highest'
set @SortOrder = 'cost_lowest'
set @SortOrder = 'org_alpha'
--set @SortOrder = 'duration_shortest'
--set @SortOrder = 'duration_longest'

-- blind search 

--set @Filter = '  (base.name like ''%western gov%'' OR base.Description like ''%western gov%''  OR base.Organization like ''%western gov%''   OR base.Url like ''%western gov%'') '

--set @Filter = ' (base.name like ''%western%'' OR base.Description like ''%western%''  OR base.Organization like ''%western%''  )'
	
--set @Filter = '  (base.Id in (SELECT c.id FROM [dbo].[Entity.FrameworkItemSummary] a inner join Entity b on a.EntityId = b.Id inner join LearningOpportunity c on b.EntityUid = c.RowId where [CategoryId] = 23 and ([CodeGroup] in ('''')  OR ([CodeId] in ('''') ) ) ) )  '
--set @Filter = '  (base.Id = 210) '
--set @Filter = ' len(Org_QAAgentAndRoles) > 0'
set @Filter = ''
set @StartPageIndex = 1
set @PageSize = 95
--set statistics time on       
EXECUTE @RC = [LearningOpportunity.ElasticSearch]
     @Filter,@SortOrder  ,@StartPageIndex  ,@PageSize, @TotalRows OUTPUT

select 'total rows = ' + convert(varchar,@TotalRows)

--set statistics time off       


<QualityAssurance><row RelationshipTypeId="2" SourceToAgentRelationship="Approved By" AgentToSourceRelationship="Approves" AgentRelativeId="209" AgentName="TESTING_(ISC)²" EntityStateId="3"/><row RelationshipTypeId="7" SourceToAgentRelationship="Offered By" AgentToSourceRelationship="Offers" AgentRelativeId="209" AgentName="TESTING_(ISC)²" EntityStateId="3"/></QualityAssurance>

<Connections><row ConnectionTypeId="6" ConnectionType="Advanced Standing For" CredentialId="1027" CredentialName="TESTING_Bachelor of Science in Nursing" AssessmentId="0" LearningOpportunityId="0"/></Connections>
*/


/* =============================================
Description:      LearningOpportunity search for elastic load
Options:

  @StartPageIndex - starting page number. 
  @PageSize - number of records on a page
  @TotalRows OUTPUT - total available rows. Used by interface to build a
custom pager
  ------------------------------------------------------
Modifications
18-01-22 mparsons - created from existing search
18-10-06 mparsons - removed [IsQARole]= 1 from QualityAssurance, so that owned and offered by can be filtered. The actual property name should also be changed now! 
*/

--exec [dbo].[LearningOpportunity.ElasticSearch] '', '', 0,0,0
ALTER PROCEDURE [dbo].[LearningOpportunity.ElasticSearch]
		@Filter           varchar(5000)
		,@SortOrder       varchar(100)
		,@StartPageIndex  int
		,@PageSize        int
		,@TotalRows       int OUTPUT

As

SET NOCOUNT ON;
-- paging
DECLARE
	@first_id         int
	,@startRow        int
	,@debugLevel      int
	,@SQL             varchar(5000)
	,@OrderBy         varchar(100)

-- =================================

Set @debugLevel = 4

print '@SortOrder ' + @SortOrder
if @SortOrder = 'relevance' set @SortOrder = 'base.Name '
else if @SortOrder = 'alpha' set @SortOrder = 'base.Name '
else if @SortOrder = 'org_alpha' set @SortOrder = 'base.Organization, base.Name '
else if @SortOrder = 'newest' set @SortOrder = 'base.lastUpdated Desc '
--else if @SortOrder = 'cost_highest' set @SortOrder = 'costs.TotalCost DESC'
--else if @SortOrder = 'cost_lowest' set @SortOrder = 'costs.TotalCost'
else set @SortOrder = 'base.Name '

if len(@SortOrder) > 0 
      set @OrderBy = ' Order by ' + @SortOrder
else
      set @OrderBy = ' Order by base.Name '


--===================================================
-- Calculate the range
--===================================================
SET @StartPageIndex =  (@StartPageIndex - 1)  * @PageSize
IF @StartPageIndex < 1        SET @StartPageIndex = 1

 
-- =================================
CREATE TABLE #tempWorkTable(
      RowNumber         int PRIMARY KEY IDENTITY(1,1) NOT NULL,
      Id int,
      Title             varchar(500)
			,Organization varchar(300)
)

-- =================================

  if len(@Filter) > 0 begin
     if charindex( 'where', @Filter ) = 0 OR charindex( 'where',  @Filter ) > 10
        set @Filter =     ' where ' + @Filter
     end

  print '@Filter len: '  +  convert(varchar,len(@Filter))
  set @SQL = 'SELECT  base.Id, base.Name , base.Organization  
        from [LearningOpportunity_Summary] base 
				--left join Entity_CostProfileTotal costs on base.RowId = costs.ParentEntityUid   
				--not ideal, but doing a total
					--left join (
					--Select ParentEntityUid, sum(isnull(TotalCost, 0)) As TotalCost from Entity_CostProfileTotal group by ParentEntityUid
					--) costs	on base.RowId = costs.ParentEntityUid 
				'
        + @Filter
        
  if charindex( 'order by', lower(@Filter) ) = 0
    set @SQL = @SQL + ' ' + @OrderBy

  print '@SQL len: '  +  convert(varchar,len(@SQL))
  print @SQL

  INSERT INTO #tempWorkTable (Id, Title, Organization)
  exec (@SQL)
  --print 'rows: ' + convert(varchar, @@ROWCOUNT)
  SELECT @TotalRows = @@ROWCOUNT
-- =================================

print 'added to temp table: ' + convert(varchar,@TotalRows)
if @debugLevel > 7 begin
  select * from #tempWorkTable
  end

-- Calculate the range
--===================================================
PRINT '@StartPageIndex = ' + convert(varchar,@StartPageIndex)

SET ROWCOUNT @StartPageIndex
--SELECT @first_id = RowNumber FROM #tempWorkTable   ORDER BY RowNumber
SELECT @first_id = @StartPageIndex
PRINT '@first_id = ' + convert(varchar,@first_id)

if @first_id = 1 set @first_id = 0
--set max to return
SET ROWCOUNT @PageSize

-- ================================= 
SELECT        
	RowNumber, 
	base.id, 
	base.Name, 
	isnull(base.Description,'') As Description
	--[OrgId],base.Organization,
	,case when Charindex('Placeholder', Isnull(base.Organization, '')) = 1 then 0
	else [OrgId] end  as [OrgId]
	,case when Charindex('Placeholder', Isnull(base.Organization, '')) = 1 then ''
	else base.Organization end  as [Organization]
	,base.OwningOrganizationCtid
	 
	,isnull(base.SubjectWebpage,'') As SubjectWebpage

	,[DateEffective]
    ,isnull(base.[IdentificationCode],'') As [IdentificationCode]

	,base.availableOnlineAt
	,base.AvailabilityListing
	,base.Created
	,base.LastUpdated 
	,e.LastUpdated As EntityLastUpdated
	,base.RowId
	,base.EntityStateId

	,base.ConnectionsList			--actual connection type (no credential info)
	,base.CredentialsList	--connection type, plus Id, and name of credential
	-- ====================================
	,base.RequiresCount
	,base.RecommendsCount
	,base.isRequiredForCount
	,base.IsRecommendedForCount
	,base.IsAdvancedStandingForCount
	,base.AdvancedStandingFromCount
	,base.isPreparationForCount
	,base.PreparationFromCount

	,IsNULL(costProfiles.Nbr, 0) As CostProfilesCount
	--remove these
	--,IsNULL(costs.TotalCost, 0) As TotalCostCount
	,0 As TotalCostCount
	,IsNull(CommonCost.Nbr,0) As CommonCostsCount
	,IsNull(CommonCondition.Nbr,0) As CommonConditionsCount
	,IsNULL(FinancialAid.Nbr, 0) As FinancialAidCount
	,IsNULL(processProfiles.Nbr, 0) As ProcessProfilesCount

	
	,IsNULL(HasCIP.Nbr, 0) As HasCIPCount
	,IsNULL(HasDuration.Nbr, 0) As HasDurationCount

	,(SELECT DISTINCT ConnectionTypeId ,ConnectionType  ,AssessmentId, isnull(AssessmentName,'') As AssessmentName,   CredentialId, IsNUll(CredentialName,'') As CredentialName, LearningOpportunityId, Isnull(LearningOpportunityName,'') As LearningOpportunityName, credOrgid,credOrganization, asmtOrgid, asmtOrganization, loppOrgid, loppOrganization  FROM [dbo].[Entity_ConditionProfilesConnectionsSummary]  WHERE EntityTypeId = 7 
		AND EntityBaseId = base.id 
		FOR XML RAW, ROOT('Connections')) LoppConnections

	--,isnull(comps.Nbr,0) as Competencies
	,0 as CompetenciesCount
	,base.CTID
	,base.CredentialRegistryId

	 ,(SELECT ISNULL(NULLIF(a.TextValue, ''), NULL) TextValue, a.[CodedNotation], a.CategoryId FROM [dbo].[Entity.SearchIndex] a inner join Entity b on a.EntityId = b.Id inner join Assessment c on b.EntityUid = c.RowId WHERE b.EntityTypeId = 7 AND c.Id = base.Id FOR XML RAW, ROOT('TextValues')) TextValues

	,(SELECT DISTINCT lcs.[Name], lcs.[Description] FROM [dbo].LearningOpportunity_Competency_Summary lcs  where lcs.LearningOpportunityId = base.Id AND lcs.AlignmentType = 'teaches' FOR XML RAW, ROOT('TeachesCompetencies')) TeachesCompetencies

	,(SELECT DISTINCT crc.TargetNodeName As Name FROM [dbo].[ConditionProfile_RequiredCompetencies] crc  where crc.ParentEntityBaseId = base.Id AND crc.ParentEntityTypeId = 7 FOR XML RAW, ROOT('RequiresCompetencies')) RequiresCompetencies

	,(SELECT DISTINCT a.[Subject] FROM [Entity_Subjects] a where EntityTypeId = 7 AND a.EntityUid = base.RowId FOR XML RAW, ROOT('SubjectAreas')) SubjectAreas

	,(SELECT a.[CategoryId], a.[ReferenceFrameworkId], a.[Name], a.[SchemaName], ISNULL(NULLIF(a.[CodeGroup], ''), NULL) CodeGroup, a.[CodedNotation] FROM [dbo].[Entity_ReferenceFramework_Summary] a inner join Entity b on a.EntityId = b.Id inner join LearningOpportunity c on b.EntityUid = c.RowId where a.[CategoryId] = 23 AND c.[Id] = base.[Id] FOR XML RAW, ROOT('Classifications')) Classifications 

	
	,(SELECT a.[CategoryId], a.[ReferenceFrameworkId], a.[Name], a.[SchemaName], ISNULL(NULLIF(a.[CodeGroup], ''), NULL) CodeGroup, a.[CodedNotation] FROM [dbo].[Entity_ReferenceFramework_Summary] a inner join Entity b on a.EntityId = b.Id inner join LearningOpportunity c on b.EntityUid = c.RowId where a.[CategoryId] IN (10, 11, 23) AND c.[Id] = base.[Id] FOR XML RAW, ROOT('Frameworks')) Frameworks 


	--,(SELECT DISTINCT a.CategoryId, a.[PropertyValueId], a.Property FROM [dbo].[EntityProperty_Summary] a where EntityTypeId= 7 AND CategoryId = 21 AND[EntityBaseId] = base.Id FOR XML RAW, ROOT('DeliveryMethodTypes')) DeliveryMethodTypes
	--,(SELECT DISTINCT a.CategoryId, a.[PropertyValueId], a.Property FROM [dbo].[EntityProperty_Summary] a where EntityTypeId= 7 AND CategoryId = 53 AND[EntityBaseId] = base.Id FOR XML RAW, ROOT('LearningMethodTypes')) LearningMethodTypes
	
	--,isnull(typesCsv.Properties,'') As TypesList
	-- , STUFF((SELECT '|' + ISNULL(NULLIF(CAST(eps.PropertyValueId AS NVARCHAR(MAX)), ''), NULL) AS [text()] 
	--	FROM [dbo].[EntityProperty_Summary] eps 
	--	where eps.EntityTypeId = 7 AND eps.CategoryId = 14 AND eps.EntityBaseId = base.Id 
	--	FOR XML Path('')), 1,1,'') AudienceTypeIds

	 , ( SELECT DISTINCT a.CategoryId, a.[PropertyValueId], a.Property, PropertySchemaName  FROM [dbo].[EntityProperty_Summary] a where EntityTypeId= 7 AND CategoryId IN ( 4, 14, 21, 53) AND base.Id = [EntityBaseId] FOR XML RAW, ROOT('LoppProperties')) LoppProperties

 	--, ( SELECT DISTINCT a.CategoryId, a.TextValue FROM [dbo].[Entity.Reference] a inner join [Entity] b on a.EntityId = b.Id where b.EntityTypeId= 7 AND a.CategoryId = 65 AND base.Id = b.[EntityBaseId] FOR XML RAW, ROOT('Languages')) Languages
	,isnull(Languages.Languages,'') As Languages	
	--=== QA ==============================
	,base.Org_QAAgentAndRoles
	--this is incorrect, it is all relationships should use RelationshipTypes for consistency
	--renamed from QualityAssurances
	--this should be obsolete
	--,( SELECT DISTINCT a.[RelationshipTypeId] FROM [dbo].[Entity.AgentRelationship] a inner join Entity b on a.EntityId = b.Id where base.RowId = b.EntityUid FOR XML RAW, ROOT('RelationshipTypes')) AgentRelationships  
	--now obsolete
	,'' as AgentRelationships
	,'' as QualityAssurances

	--all entity to organization relationships with org information. 
	 ,(SELECT DISTINCT AgentRelativeId As OrgId, AgentName, AgentUrl, EntityStateId, RoleIds as RelationshipTypeIds,  Roles as Relationships, AgentContextRoles FROM [dbo].[Entity.AgentRelationshipIdCSV]
			WHERE EntityTypeId= 7 AND EntityBaseId = base.id 
			FOR XML RAW, ROOT('AgentRelationshipsForEntity')) AgentRelationshipsForEntity


	--[IsQARole]= 1 and 
	,(SELECT DISTINCT [RelationshipTypeId] ,[SourceToAgentRelationship]   ,[AgentToSourceRelationship]  ,[AgentRelativeId], Isnull([AgentUrl],'') As AgentUrl , AgentName, IsQARole, [EntityStateId] 
		FROM [dbo].[Entity_Relationship_AgentSummary]  
		WHERE [SourceEntityTypeId] = 7 AND SourceEntityUid = base.Rowid 
		FOR XML RAW, ROOT('QualityAssurance')) QualityAssurance

	--[IsQARole]= 1 and 
	,(SELECT b.RowId, b.Id, b.EntityId, a.EntityUid, a.EntityTypeId, a.EntityBaseId, a.EntityBaseName, b.Id AS EntityAddressId, b.Name, b.IsPrimaryAddress, b.Address1, b.Address2, b.City, b.Region, b.PostOfficeBoxNumber, b.PostalCode, b.Country, b.Latitude, b.Longitude, b.Created, b.LastUpdated FROM dbo.Entity AS a INNER JOIN dbo.[Entity.Address] AS b ON a.Id = b.EntityId where a.[EntityUid] = base.[RowId] FOR XML RAW, ROOT('Addresses')) Addresses

	-- addresses for owning org - will only be used if there is no address for the credential
	, (SELECT b.RowId, b.Id, b.EntityId, a.EntityUid, a.EntityTypeId, a.EntityBaseId, a.EntityBaseName, b.Id AS EntityAddressId, b.Name, b.IsPrimaryAddress, b.Address1, b.Address2, b.City, b.Region, b.PostOfficeBoxNumber, b.PostalCode, b.Country, b.Latitude, b.Longitude, b.Created, b.LastUpdated FROM dbo.Entity AS a INNER JOIN dbo.[Entity.Address] AS b ON a.Id = b.EntityId where a.[EntityUid] = base.OwningAgentUid
		FOR XML RAW, ROOT('OrgAddresses')) OrgAddresses

From #tempWorkTable work
	Inner join LearningOpportunity_Summary base on work.Id = base.Id
	Inner Join Entity e on base.RowId = e.EntityUid


	left join (
		Select b.EntityBaseId, COUNT(*)  As Nbr from [Entity.CostProfile] a Inner join Entity b ON a.EntityId = b.Id Where b.EntityTypeId = 7  Group By b.EntityBaseId
	) costProfiles	on base.Id = costProfiles.EntityBaseId  

	left join (
	Select b.EntityBaseId, COUNT(*)  As Nbr from [Entity.CommonCost] a Inner join Entity b ON a.EntityId = b.Id Where b.EntityTypeId = 7  Group By b.EntityBaseId
	) CommonCost	on base.Id = CommonCost.EntityBaseId     
	left join (
	Select b.EntityBaseId, COUNT(*)  As Nbr from [Entity.CommonCondition] a Inner join Entity b ON a.EntityId = b.Id Where b.EntityTypeId = 7  Group By b.EntityBaseId
	) CommonCondition	on base.Id = CommonCondition.EntityBaseId  
	left join (
	Select b.EntityBaseId, COUNT(*)  As Nbr from [Entity.FinancialAlignmentProfile] a Inner join Entity b ON a.EntityId = b.Id Where b.EntityTypeId = 7 Group By b.EntityBaseId 
	) FinancialAid	on base.Id = FinancialAid.EntityBaseId
	left join (
	Select b.EntityBaseId, COUNT(*)  As Nbr from [Entity.ProcessProfile] a Inner join Entity b ON a.EntityId = b.Id Where b.EntityTypeId = 7  Group By b.EntityBaseId
	) processProfiles	on base.Id = processProfiles.EntityBaseId    
	left join (
	Select b.EntityBaseId, COUNT(*)  As Nbr from [Entity.ReferenceFramework] a Inner join Entity b ON a.EntityId = b.Id Where b.EntityTypeId = 7  Group By b.EntityBaseId 
	) HasCIP	on base.Id = HasCIP.EntityBaseId  
	left join (
	Select b.EntityBaseId, COUNT(*)  As Nbr from [Entity.DurationProfile] a Inner join Entity b ON a.EntityId = b.Id Where b.EntityTypeId = 7  Group By b.EntityBaseId 
	) HasDuration	on base.Id = HasDuration.EntityBaseId  
	--left join EntityProperty_AudienceTypeCSV typesCsv on base.Id = typesCsv.EntityId
	left Join ( 
		SELECT     distinct base.Id, 
		CASE WHEN Languages IS NULL THEN ''           WHEN len(Languages) = 0 THEN ''          ELSE left(Languages,len(Languages)-1)     END AS Languages
		From dbo.LearningOpportunity base
		CROSS APPLY ( SELECT a.TextValue + '| '
			FROM [dbo].[Entity.Reference] a inner join [Entity] b on a.EntityId = b.Id 
			where b.EntityTypeId= 7 AND a.CategoryId = 65
			and base.Id = b.EntityBaseId FOR XML Path('')  ) D (Languages)
		where Languages is not null
	) Languages on base.Id = Languages.Id
WHERE RowNumber > @first_id
order by RowNumber 
go

grant execute on [LearningOpportunity.ElasticSearch] to public
go
