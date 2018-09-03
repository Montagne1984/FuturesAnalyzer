using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Security;
using CsvHelper.Configuration;
using Dlw.ScBase.Common.Infrastructure.Collections;
using Dlw.ScBase.Common.Infrastructure.Logging;
using Dlw.ScBase.Content.Cms.Config.WeChat;
using Dlw.ScBase.Content.Cms.Globalization;
using Dlw.ScBase.Content.Cms.SiteTree;
using Dlw.ScBase.Content.eCampus.Analytics.BaseHelper;
using Dlw.ScBase.Content.eCampus.CustomData.MultiChannelUserRelationship;
using Dlw.ScBase.Content.eCampus.Services;
using Dlw.ScBase.Website.Infrastructure.Config;
using Dlw.ScBase.Website.Infrastructure.Ioc;
using MongoDB.Bson;
using MongoDB.Driver;
using Sitecore.Analytics.Data.DataAccess.MongoDb;
using Sitecore.Security.Accounts;
using Sitecore.Configuration;
using Sitecore.Data;
using Dlw.ScBase.Content.Ctx;
using MongoDB.Driver.Builders;
using CsvHelper.TypeConversion;
using Sitecore.Shell.Applications.MarketingAutomation.Extensions;

namespace Dlw.ScBase.Website.Components.eCampus._Shared.Models
{
    /// <summary>
    /// Get HCP Data 
    /// </summary>
    public static class ExportDataHelper
    {
        private static IWeChatPublicAccountStore WeChatPublicAccountStore => WebContainer.Resolve<IWeChatPublicAccountStore>();
        private static IUserRelationshipService UserRelationshipService => WebContainer.Resolve<IUserRelationshipService>();
        private static readonly ILogger Log = LogProvider.GetLogger(typeof(ExportDataHelper));
        private static readonly Database MasterDb = Factory.GetDatabase("master");
        private const string ScRootId = "{26E8AC4C-6F40-4544-AE84-23DF746BE4D6}";
        private static readonly ContentStore ContentStoreHelper;
        private static readonly DataContext DataContext;

        static ExportDataHelper()
        {
            ContentStoreHelper = new ContentStore(AppSettings.SitecoreContentDatabaseName);
            var site = new Site(Factory.GetSite("Sanf"), new SiteConfiguration());
            DataContext = new DataContext(site, new Locale(site.SiteContext.ContentLanguage));
        }

        public static List<MasterDataDto> GetHcpMasterData(string targetChannel = null)
        {
            var hcpMasterData = new List<MasterDataDto>();

            var users = new List<User>();
            if (!string.IsNullOrEmpty(targetChannel))
            {
                List<string> contactIdentifies = null;
                var query = Query.And(Query.Exists("Identifiers.Identifier"),
                    Query.Exists("Personal.FirstName"),
                    Query.Or(Query.EQ("Personal.Registry.Channel", targetChannel),
                             Query.EQ("Personal.Channel", targetChannel)));
                var contacts = QueryContacts(query);
                contactIdentifies = contacts.Select(c => c.Key).ToList();
                users = contactIdentifies.Select(c => User.FromName(c, true)).ToList();
                Log.Info("ExportDataHelper - GetHcpMasterData -  Filter user by MongoDB - Done");
                Log.Info("ExportDataHelper - GetHcpMasterData - user count: " + users.Count);
            }
            else
            {
                var filterUserByRoles = new List<User>();

                IEnumerable<User> filterUserByIncompleteUser =
                    RolesInRolesManager.GetUsersInRole(Role.FromName($"extranet\\{CommonConstants.UserRole.IncompleteUser}"), true);
                IEnumerable<User> filterUserByBasicUser =
                    RolesInRolesManager.GetUsersInRole(Role.FromName($"extranet\\{CommonConstants.UserRole.BasicUser}"), true);
                IEnumerable<User> filterUserByAdvanceUser =
                    RolesInRolesManager.GetUsersInRole(Role.FromName($"extranet\\{CommonConstants.UserRole.AdvanceUser}"), true);

                filterUserByRoles.AddRange(filterUserByIncompleteUser);
                filterUserByRoles.AddRange(filterUserByBasicUser);
                filterUserByRoles.AddRange(filterUserByAdvanceUser);
                users = filterUserByRoles.DistinctBy(u => u.Name).ToList();

                Log.Info("ExportDataHelper - GetHcpMasterData -  Filter user by Roles - Done");
                Log.Info("ExportDataHelper - GetHcpMasterData - user count: " + users.Count);
            }


            //var users = Membership.GetAllUsers(); 

            var filterUserByDomains = users.Where(u => !u.GetDomainName().ToLower().StartsWith("default")
                                                                   && !u.GetDomainName().ToLower().StartsWith("sitecore")
                                                                   && !u.GetDomainName().ToLower().Contains("anonymous")
                                                                   && !u.GetDomainName().ToLower().Contains("dam")
                                                                   && ((!string.IsNullOrEmpty(targetChannel) && (u.IsInRole($"extranet\\{CommonConstants.UserRole.IncompleteUser}")
                                                                                                            || u.IsInRole($"extranet\\{CommonConstants.UserRole.BasicUser}")
                                                                                                            || u.IsInRole($"extranet\\{CommonConstants.UserRole.AdvanceUser}")))
                                                                        || string.IsNullOrEmpty(targetChannel))
                                                                  );

            var filterUsersResult = filterUserByDomains.ToList();
            Log.Info("ExportDataHelper - GetHcpMasterData - Filter domain default,sitecore anonymous and dam - done: user count: " + filterUsersResult.Count);

            var contactHelper = new ContactHelper();
            foreach (User sitecoreUser in filterUsersResult)
            {
                try
                {
                    //if (user.UserName.ToLower().StartsWith("default") || user.UserName.ToLower().StartsWith("sitecore")
                    //    || user.UserName.ToLower().Contains("anonymous"))
                    //{
                    //    Log.Info($"ExportDataHelper - GetHcpMasterData - {user.UserName} end for not match.");
                    //    continue;
                    //}
                    var user = Membership.GetUser(sitecoreUser.Name);
                    Log.Info($"ExportDataHelper - GetHcpMasterData - {user.UserName} start.");
                    var userId = user.UserName;

                    var status = 0;
                    //var sitecoreUser = User.FromName(userId, true);

                    //if (sitecoreUser != null)
                    //{
                    //    //Filter by roles: extranet\Incomplete User, extranet\Basic User, extranet\Advance User 
                    //    if (!(sitecoreUser.IsInRole($"extranet\\{CommonConstants.UserRole.IncompleteUser}") ||
                    //          sitecoreUser.IsInRole($"extranet\\{CommonConstants.UserRole.BasicUser}") ||
                    //          sitecoreUser.IsInRole($"extranet\\{CommonConstants.UserRole.AdvanceUser}")))
                    //    {
                    //        Log.Info($"ExportDataHelper - GetHcpMasterData {user.UserName} end for role is not match.");
                    //        continue;
                    //    }


                    //}

                    if (sitecoreUser.IsInRole($"extranet\\{CommonConstants.UserRole.IncompleteUser}"))
                    {
                        status = 1;
                    }
                    else if (sitecoreUser.IsInRole($"extranet\\{CommonConstants.UserRole.BasicUser}"))
                    {
                        status = 2;
                    }
                    else if (sitecoreUser.IsInRole($"extranet\\{CommonConstants.UserRole.AdvanceUser}"))
                    {
                        status = 3;
                    }

                    if (FilterUser(userId, contactHelper))
                    {
                        Log.Info($"ExportDataHelper - GetHcpMasterData - {user.UserName} - exist for filtered user.");
                        continue;
                    }

                    var personalInfo = contactHelper.GetContactPersonalInfobyIdentifier(userId);

                    var channel = personalInfo?.Registry?.Channel ?? personalInfo?.Channel;
                    if (targetChannel != null && channel != targetChannel)
                    {
                        Log.Info(
                            $"ExportDataHelper - GetHcpMasterData - {user.UserName} end for channel {channel} not match.");
                        continue;
                    }
                    Log.Info($"ExportDataHelper - GetHcpMasterData - {user.UserName} - {channel}.");


                    var weChatPublicAccount = WeChatPublicAccountStore.GetWeChatPublicAccountByChannel(channel);
                    Log.Info(
                        $"ExportDataHelper - GetHcpMasterData - {user.UserName} - for WeChat public account {weChatPublicAccount?.AuthorizationAppId}.");

                    var mappings = UserRelationshipService.GetUserRelationshipMappingsByUserName(userId).ToList();
                    var mapping = mappings.FirstOrDefault(m => m.AppId == weChatPublicAccount?.AuthorizationAppId) ??
                                  mappings.FirstOrDefault();
                    Log.Info($"ExportDataHelper - GetHcpMasterData - {user.UserName} - {mapping?.OpenId}.");

                    if (user.ProviderUserKey != null)
                    {
                        Log.Info(
                            $"ExportDataHelper - GetHcpMasterData - {user.UserName} - Generate MasterDataDto start.");
                        var masterDataDto = new MasterDataDto
                        {
                            UserName = personalInfo?.FirstName,
                            HcpID = ((Guid)user.ProviderUserKey).ToString(),
                            WeChatOpenId = mapping?.OpenId,
                            WeChatAccount =
                                weChatPublicAccount?.AccountName ?? WeChatPublicAccountStore.GetWeChatPublicAccounts()?
                                    .FirstOrDefault(w => w.AppId.Equals(personalInfo?.AppId))?.AccountName,
                            EtmsCode = personalInfo?.EtmsCode,
                            Province = personalInfo?.Province?.FirstOrDefault(),
                            City = personalInfo?.City?.FirstOrDefault(),
                            Department = personalInfo?.Department?.FirstOrDefault(),
                            JobTitle = personalInfo?.HcpTitle?.FirstOrDefault(),
                            Email = user.Email,
                            MobileNumber = personalInfo?.HcpMobile,
                            Hospital = personalInfo?.Hospital?.FirstOrDefault(),
                            HospitalCode = personalInfo?.HospitalCode,
                            ChannelAppId = weChatPublicAccount?.AppId,
                            AuthorizationAppId = mapping?.AppId,
                            UnionId = personalInfo?.UnionId,
                            Status = status,
                            RegisterType = personalInfo?.Registry?.Type ?? personalInfo?.RegisterSource,
                            RegisterDate = user.CreationDate.AddHours(8).ToString("yyyy/MM/dd HH:mm:ss")
                        };
                        Log.Info($"ExportDataHelper - GetHcpMasterData - {user.UserName} - Generate MasterDataDto end.");

                        if ((string.IsNullOrWhiteSpace(masterDataDto.HospitalCode) ||
                             masterDataDto.HospitalCode.StartsWith("Dlw.ScBase.Content")) &&
                            !string.IsNullOrWhiteSpace(masterDataDto.Province) &&
                            !string.IsNullOrWhiteSpace(masterDataDto.City))
                        {
                            masterDataDto.HospitalCode = GetHospitalCode(masterDataDto.Province, masterDataDto.City,
                                masterDataDto.Hospital);
                        }
                        Log.Info($"ExportDataHelper - GetHcpMasterData  - {user.UserName} - Generate MasterDataDto end.");
                        //?? (string.IsNullOrEmpty(masterDataDto.Hospital)?
                        //string.Empty:HcpLocationModel.GetHospitalCodeByName(masterDataDto.Hospital));
                        hcpMasterData.Add(masterDataDto);
                        Log.Info(
                            $"ExportDataHelper - GetHcpMasterData - {user.UserName} - Generate MasterDataDto added.");

                        Log.Info($"ExportDataHelper - GetHcpMasterData - {user.UserName} end.");
                    }
                }
                catch (Exception ex)
                {
                    Log.Info(ex.StackTrace);
                    Log.Error($"ExportDataHelper - GetHcpMasterData - Exception - {ex.Message}");
                }
            }
            Log.Info("ExportDataHelper - GetHcpMasterData - hcpMasterData count" + hcpMasterData.Count);

            return hcpMasterData;
        }

        public static List<CustomDbHelper.AnalyticsVisitDto> GetVisitDataByDateTime(DateTime startDate, DateTime endDate)
        {
            var visitData = new List<CustomDbHelper.AnalyticsVisitDto>();
            var dbHelper = new CustomDbHelper();
            Log.Info($"GetVisitDataByDateTime - Start GetVisitDataByDateTime {startDate} to {endDate}...");
            var trackingData = dbHelper.GetVisitDataByDateTime(startDate, endDate).ToList();
            Log.Info("GetVisitDataByDateTime - End GetVisitDataByDateTime.");
            var contentItemIds = new Dictionary<string, Guid>();

            var contacts = GetRegisteredContacts();
            if (!contacts.Any())
            {
                Log.Info("No valid contacts found in xDB.");
                return visitData;
            }
            var contactIds = contacts.Select(c => c.Value["_id"]?.AsGuid).ToList();

            var weChatPublicAccounts = new Dictionary<string, string>();

            foreach (var trackingItem in trackingData)
            {
                var contactId = trackingItem.ContactId;
                Log.Info("InteractionId: " + trackingItem.InteractionId);
                if (!contactIds.Contains(contactId))
                {
                    Log.Info($"GetVisitDataByDateTime - Not a registered user {contactId}");
                    continue;
                }

                var contact = contacts[contactId];
                var identifier =
                    contact?.Elements?.FirstOrDefault(el => el.Name.Equals("Identifiers"))?
                        .Value?.AsBsonDocument?.Elements?.FirstOrDefault(b => b.Name.Equals("Identifier"))?
                        .Value?.AsString;

                Log.Info("GetVisitDataByDateTime - identifier " + identifier);
                var user = string.IsNullOrWhiteSpace(identifier) ? null : Membership.GetUser(identifier);
                if (user != null)
                {
                    if (FilterUser(identifier, contact, weChatPublicAccounts))
                    {
                        contacts[contactId] = null;
                        Log.Info("not a valid user");
                        continue;
                    }

                    Log.Info("GetVisitDataByDateTime - url " + trackingItem.Url.Replace("{", "{{").Replace("}", "}}"));
                    var urlParts = trackingItem.Url.Split('/');

                    if (urlParts.Length >= 2 && (
                        urlParts[urlParts.Length - 2].ToLower() == "news" ||
                        urlParts[urlParts.Length - 2].ToLower() == "video" ||
                        urlParts[urlParts.Length - 2].ToLower() == "ematerial" ||
                        urlParts[urlParts.Length - 2].ToLower() == "meeting" ||
                        urlParts[urlParts.Length - 2].ToLower() == "medicalinfo" ||
                        urlParts[urlParts.Length - 2].ToLower() == "summit" ||
                        urlParts[urlParts.Length - 2].ToLower() == "lecture" ||
                        urlParts[urlParts.Length - 2].ToLower() == "about-us"))
                    {
                        if (!contentItemIds.ContainsKey(trackingItem.Url))
                        {
                            contentItemIds.Add(trackingItem.Url,
                                ContentStoreHelper.GetItemIdByUrl(DataContext, trackingItem.Url).Guid);
                        }
                        trackingItem.SitecoreItemId = contentItemIds[trackingItem.Url];
                    }
                    trackingItem.HcpId = ((Guid?)user.ProviderUserKey)?.ToString();
                    //ticket I-224678
                    visitData.Add(trackingItem);
                }
                //ticket I-224678
                //visitData.Add(trackingItem);
            }
            Log.Info("GetVisitDataByDateTime - End.");
            return visitData;
        }

        private static Dictionary<Guid, BsonDocument> GetRegisteredContacts()
        {
            var query = Query.And(Query.Exists("Identifiers.Identifier"),
                Query.Exists("Personal.FirstName"));
            var driver = MongoDbDriver.FromConnectionString("analytics");
            var contacts = driver.Contacts.FindAs<BsonDocument>(query)
                .EmptyWhenNull().FilterNulls()
                .ToDictionary(x => x["_id"].AsGuid);
            return contacts;
        }

        public static List<CustomDbHelper.AnalyticsVisitDto> GetScVisitDataByDateTime(DateTime startDate, DateTime endDate, bool includeAnonymousUser = false)
        {
            var visitData = new List<CustomDbHelper.AnalyticsVisitDto>();
            var dbHelper = new CustomDbHelper();
            Log.Info($"GetVisitDataByDateTime - Start GetScVisitDataByDateTime {startDate} to {endDate}...");
            var trackingData = dbHelper.GetVisitDataByDateTime(startDate, endDate).ToList();
            Log.Info("GetVisitDataByDateTime - End GetScVisitDataByDateTime.");
            var scRoot = MasterDb.GetItem(new ID(ScRootId));
            var contentItemIds = new Dictionary<string, Guid>();

            Dictionary<Guid, BsonDocument> contacts = null;
            List<Guid?> contactIds = null;
            if (!includeAnonymousUser)
            {
                contacts = GetRegisteredContacts();
                if (!contacts.Any())
                {
                    Log.Info("No valid contacts found in xDB.");
                    return visitData;
                }
                contactIds = contacts.Select(c => c.Value["_id"]?.AsGuid).ToList();
            }

            foreach (var trackingItem in trackingData)
            {
                if (string.IsNullOrWhiteSpace(trackingItem.Url))
                {
                    continue;
                }
                try
                {
                    Log.Info("GetScVisitDataByDateTime - " + trackingItem.VisitUniqueId);
                    if (!trackingItem.Url.ToLower().Contains("/sc/") &&
                        !trackingItem.Url.ToLower().Contains("/likecontent") &&
                        !trackingItem.Url.ToLower().Contains("/addfavoritecontent"))
                    {
                        Log.Info("GetScVisitDataByDateTime -  not SC visit");
                        continue;
                    }
                    trackingItem.EntryTime = trackingItem.EntryTime.AddHours(AppSettings.TimeDifference);
                    var contactId = trackingItem.ContactId;
                    if (!includeAnonymousUser && !contactIds.Contains(contactId))
                    {
                        Log.Info($"GetScVisitDataByDateTime - {contactId} not contained");
                        continue;
                    }

                    if (!includeAnonymousUser)
                    {
                        var contact = contacts[contactId];
                        var identifier =
                            contact?.Elements?.FirstOrDefault(el => el.Name.Equals("Identifiers"))?
                                .Value?.AsBsonDocument?.Elements?.FirstOrDefault(b => b.Name.Equals("Identifier"))?
                                .Value?.AsString;
                        var user = string.IsNullOrWhiteSpace(identifier) ? null : Membership.GetUser(identifier);
                        if (user == null)
                        {
                            Log.Info($"GetScVisitDataByDateTime - Not a registered user {contactId}");
                            continue;
                        }

                        trackingItem.HcpId = ((Guid?)user.ProviderUserKey)?.ToString();
                        trackingItem.HcpName = User.FromName(user.UserName, true)?.Profile?.FullName;

                        if (string.IsNullOrWhiteSpace(trackingItem.HcpName))
                        {
                            Log.Info($"GetScVisitDataByDateTime - no HCP name {contactId}");
                            continue;
                        }
                    }

                    if (!trackingItem.SitecoreItemId.HasValue)
                    {
                        Log.Info("GetScVisitDataByDateTime - no item ID");
                        continue;
                    }

                    var urlParts = trackingItem.Url.Split('/');
                    if (urlParts.Length < 3)
                    {
                        Log.Info("GetScVisitDataByDateTime - not an article");
                        continue;
                    }

                    if (urlParts[urlParts.Length - 2].ToLower() == "news" ||
                        urlParts[urlParts.Length - 2].ToLower() == "video" ||
                        urlParts[urlParts.Length - 2].ToLower() == "ematerial")
                    {
                        if (!contentItemIds.ContainsKey(trackingItem.Url))
                        {
                            var itemId = ContentStoreHelper.GetItemIdByUrl(DataContext, trackingItem.Url)?.Guid;
                            if (!itemId.HasValue)
                            {
                                Log.Info("GetScVisitDataByDateTime - no item ID");
                                continue;
                            }
                            contentItemIds.Add(trackingItem.Url, itemId.Value);
                        }
                        trackingItem.SitecoreItemId = contentItemIds[trackingItem.Url];
                    }

                    var item = MasterDb.GetItem(new ID(trackingItem.SitecoreItemId.Value));

                    if (!item.Paths.IsDescendantOf(scRoot))
                    {
                        Log.Info("GetScVisitDataByDateTime - not SC item");
                        continue;
                    }
                    if (string.IsNullOrWhiteSpace(item["Title"]))
                    {
                        Log.Info("GetScVisitDataByDateTime - empty content title");
                        continue;
                    }
                    trackingItem.ContentTitle = item["Title"];

                    visitData.Add(trackingItem);
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                }
            }
            return visitData;
        }

        private static bool FilterUser(string identifier, ContactHelper contactHelper)
        {
            Log.Info($"FilterUser {identifier}");
            var sitecoreUser = User.FromName(identifier, true);
            if (sitecoreUser.IsInRole(Role.FromName(AppSettings.FilterRoleForOds))) return true;

            var personalInfo = contactHelper.GetContactPersonalInfobyIdentifier(identifier);
            var channel = personalInfo?.Registry?.Channel ?? personalInfo?.Channel;
            var weChatPublicAccount = WeChatPublicAccountStore.GetWeChatPublicAccountByChannel(channel);
            var openId = contactHelper.GetOpenId(personalInfo, weChatPublicAccount?.AuthorizationAppId);

            //exclude wechat  without openid user
            if (weChatPublicAccount != null && string.IsNullOrEmpty(openId))
            {
                return true;
            }

            if (string.IsNullOrEmpty(personalInfo?.FirstName))
            {
                return true;
            }
            return false;
        }

        private static bool FilterUser(string identifier, BsonDocument contact,
            Dictionary<string, string> weChatPublicAccounts)
        {
            Log.Info($"FilterUser {identifier}");
            //var sitecoreUser = User.FromName(identifier, true);
            //if (sitecoreUser.IsInRole(Role.FromName(AppSettings.FilterRoleForOds))) return true;

            var personalInfo =
                contact?.Elements?.FirstOrDefault(el => el.Name.Equals("Personal"))?
                    .Value?.AsBsonDocument;

            var firstName =
                personalInfo?.Elements?.FirstOrDefault(el => el.Name.Equals("FirstName"))?
                    .Value?.AsString;

            if (string.IsNullOrEmpty(firstName))
            {
                Log.Info("empty first name");
                return true;
            }

            var channel =
                personalInfo.Elements?.FirstOrDefault(el => el.Name.Equals("Registry"))?
                    .Value?.AsBsonDocument?.Elements?.FirstOrDefault(b => b.Name.Equals("Channel"))?
                    .Value?.AsString ?? personalInfo.Elements?.FirstOrDefault(el => el.Name.Equals("Channel"))?
                        .Value?.AsString;

            if (channel == null)
            {
                return false;
            }
            if (!weChatPublicAccounts.ContainsKey(channel))
            {
                var weChatPublicAccount = WeChatPublicAccountStore.GetWeChatPublicAccountByChannel(channel);
                if (weChatPublicAccount == null)
                {
                    return false;
                }
                weChatPublicAccounts.Add(channel, weChatPublicAccount.AuthorizationAppId);
            }

            var appId = weChatPublicAccounts[channel];
            var openId =
                personalInfo.Elements?.FirstOrDefault(el => el.Name.Equals("WeChat Binding"))?
                    .Value?.AsBsonDocument?.Elements?.FirstOrDefault(
                        b => b.Name.Equals(appId))?
                    .Value?.AsString ?? personalInfo.Elements?.FirstOrDefault(el => el.Name.Equals("OpenId"))?
                        .Value?.AsString;

            if (string.IsNullOrEmpty(openId))
            {
                Log.Info("no openid for " + appId);
                return true;
            }

            return false;
        }

        public static string GetHospitalCode(string province, string city, string hospital)
        {
            if (string.IsNullOrWhiteSpace(province) || string.IsNullOrWhiteSpace(city) ||
                string.IsNullOrWhiteSpace(hospital))
            {
                return string.Empty;
            }

            var hospitalItem =
                Factory.GetDatabase("master").SelectSingleItem(
                    $"fast:/sitecore/content/Sites/eCampus/Site Data/External Register/Location/*[@Name='{province}']/*[@Name='{city}']//*[@Name='{hospital}']");
            return hospitalItem != null ? hospitalItem.Name : string.Empty;
        }

        /// <summary>
        /// Query contacts through native MongoDB Driver API
        /// For peroformance consideration, we only need the contact information without interactions, automations, etc.
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        public static Dictionary<string, BsonDocument> QueryContacts(IMongoQuery query)
        {
            var driver = MongoDbDriver.FromConnectionString("analytics");
            var contacts = driver.Contacts.FindAs<BsonDocument>(query)
                .EmptyWhenNull().FilterNulls()
                .ToDictionary(x => x["Identifiers"]?["Identifier"]?.AsString);

            return contacts;
        }

        #region Bizconf

        public static List<BizconfBehaviorDto> GetBizconfBehaviorData(DateTime startDate, DateTime endDate, string bu)
        {
            var contactHelper = new ContactHelper();
            var rawData = GetBizconfRawData(startDate, endDate, bu);
            var liveFlagIndex = 1;
            foreach (var data in rawData)
            {
                if (data.LiveFlag == "直播")
                {
                    data.LiveFlagIndex = 0;
                }
                else
                {
                    data.LiveFlagIndex = liveFlagIndex;
                    liveFlagIndex++;
                }
            }
            var groups = rawData.GroupBy(d => new { d.MeetingId, d.Uid, d.LiveFlag, d.LiveFlagIndex, d.Name });
            var bizConfBehaviorData = new List<BizconfBehaviorDto>();
            foreach (var group in groups)
            {
                var dataWithMaxViewerCount = group.OrderByDescending(d => d.ViewerCount).First();
                var viewerBu = dataWithMaxViewerCount.Bu;
                var district = string.Empty;
                var province = dataWithMaxViewerCount.Province;
                var city = dataWithMaxViewerCount.City;
                var county = dataWithMaxViewerCount.County;
                var hospital = dataWithMaxViewerCount.Hospital;
                var name = dataWithMaxViewerCount.Name;
                var personalInfo = string.IsNullOrWhiteSpace(dataWithMaxViewerCount.Uid?.ToString()) ? null : contactHelper.GetContactPersonalInfobyIdentifier("extranet\\" + dataWithMaxViewerCount.Uid);
                var code = personalInfo?.EtmsCode;
                var role = dataWithMaxViewerCount.Role;
                var uid = personalInfo?.UnionId;
                var email = dataWithMaxViewerCount.Email;
                var mobile = personalInfo?.HcpMobile;
                var joinTime = group.Min(d => d.FirstLoginTime);
                var leaveTime = group.Max(d => d.LastOnlineTime);
                var viewDuration = group.Sum(d => d.ViewDuration);
                var viewerCount = dataWithMaxViewerCount.ViewerCount;
                var loginCount = group.Count();
                var longestViewDuration = group.Max(d => d.ViewDuration);
                var viewChannel = dataWithMaxViewerCount.LastLoginDevice == "电脑端" ? "电脑" : (dataWithMaxViewerCount.LastLoginDevice == "移动端" ? "微信" : string.Empty);
                var meetingBu = dataWithMaxViewerCount.Bu;
                var meetingName = dataWithMaxViewerCount.MeetingName;
                var meetingId = dataWithMaxViewerCount.MeetingId;
                var meetingTime = dataWithMaxViewerCount.MeetingTime;
                var product = dataWithMaxViewerCount.Product;
                var project = dataWithMaxViewerCount.Project;
                var field = dataWithMaxViewerCount.Field;
                var comment = string.Join("|", group.Select(d => d.Comment).ToArray());
                var liveFlag = dataWithMaxViewerCount.LiveFlag;

                bizConfBehaviorData.Add(new BizconfBehaviorDto
                {
                    ViewerBu = viewerBu,
                    District = county,
                    Province = province,
                    City = city,
                    County = county,
                    Hospital = hospital,
                    Name = name,
                    Role = role,
                    Code = code,
                    Uid = uid,
                    Email = email,
                    Mobile = mobile,
                    JoinTime = joinTime,
                    LeaveTime = leaveTime,
                    ViewDuration = viewDuration,
                    ViewerCount = viewerCount,
                    LoginCount = loginCount,
                    LongestViewDuration = longestViewDuration,
                    ViewChannel = viewChannel,
                    MeetingBu = meetingBu,
                    MeetingName = meetingName,
                    MeetingId = meetingId,
                    MeetingTime = meetingTime,
                    Product = product,
                    Project = project,
                    Field = field,
                    Comment = comment,
                    LiveFlag = liveFlag
                });
            }
            return bizConfBehaviorData;
        }

        public static List<CustomDbHelper.BizconfData> GetBizconfRawData(DateTime startDate, DateTime endDate, string bu)
        {
            var customDbHelper = new CustomDbHelper();
            return customDbHelper.GetBizconfData(startDate, endDate, bu);
        }
        #endregion
    }

    public class MasterDataDto
    {
        public string HcpID { get; set; }
        public string EtmsCode { get; set; }
        public string WeChatAccount { get; set; }
        public string WeChatOpenId { get; set; }
        public string UserName { get; set; }
        public string Province { get; set; }
        public string City { get; set; }
        public string Hospital { get; set; }
        public string HospitalCode { get; set; }
        public string Department { get; set; }
        public string JobTitle { get; set; }
        public string Email { get; set; }
        public string MobileNumber { get; set; }
        public string Supplier => "Delaware";
        public string Action => "I";
        public string ChannelAppId { get; set; }
        public string AuthorizationAppId { get; set; }
        public string UnionId { get; set; }
        public int Status { get; set; }
        public int Consent => 1;
        public string RegisterType { get; set; }
        public string RegisterDate { get; set; }
    }

    public sealed class WeChatShareClickSummaryMap : CsvClassMap<CustomDbHelper.WeChatShareClickSummaryData>
    {
        public WeChatShareClickSummaryMap()
        {
            Map(m => m.ItemId).Name("Content Id");
            Map(m => m.Title).Name("Title");
            Map(m => m.Link).Name("Link");
            Map(m => m.Site).Name("Site");
            Map(m => m.ShareCount).Name("Share Count");
            Map(m => m.ClickCount).Name("Open Count");
        }
    }

    public sealed class UserMap : CsvClassMap<MasterDataDto>
    {
        public UserMap()
        {
            Map(m => m.HcpID).Name("HCPId");
            Map(m => m.UserName).Name("Name");
            Map(m => m.Province).Name("Province");
            Map(m => m.City).Name("City");
            Map(m => m.Hospital).Name("Hospital");
            Map(m => m.HospitalCode).Name("HospitalCode");
            Map(m => m.Department).Name("Department");
            Map(m => m.EtmsCode).Name("ETMSCode");
            Map(m => m.Email).Name("Email");
            Map(m => m.MobileNumber).Name("Mobile");
            Map(m => m.ChannelAppId).Name("channelAppId");
            Map(m => m.AuthorizationAppId).Name("appId");
            Map(m => m.WeChatOpenId).Name("openID");
            Map(m => m.UnionId).Name("unionID");
            Map(m => m.Status).Name("Status");
            Map(m => m.Consent).Name("Consent");
            Map(m => m.RegisterType).Name("RegisterType");
        }
    }

    public sealed class ScTrackingMap : CsvClassMap<CustomDbHelper.AnalyticsVisitDto>
    {
        public ScTrackingMap()
        {
            Map(m => m.HcpName).Name("HCP name");
            Map(m => m.HcpId).Name("HCP ID");
            Map(m => m.ContentTitle).Name("Content Title");
            Map(m => m.Duration).Name("Duration");
            Map(m => m.EntryTime).Name("Date").TypeConverter<DateTimeConverter>();
            Map(m => m.IsLike).Name("Like").TypeConverter<SitecoreExtensions.Tasks.BooleanConverter>();
            Map(m => m.IsFavorite).Name("Favorite").TypeConverter<SitecoreExtensions.Tasks.BooleanConverter>();
        }
    }

    public sealed class ScContentMap : CsvClassMap<ContentDto>
    {
        public ScContentMap()
        {
            Map(m => m.PageTitle).Name("Content Title");
            Map(m => m.Tags).Name("Tag");
            Map(m => m.VisitCount).Name("Times");
            Map(m => m.Duration).Name("Duration");
        }
    }

    public sealed class ScHcpMap : CsvClassMap<MasterDataDto>
    {
        public ScHcpMap()
        {
            Map(m => m.UserName).Name("HCP Name");
            Map(m => m.HcpID).Name("HCP ID");
            Map(m => m.WeChatOpenId).Name("OpenID");
            Map(m => m.EtmsCode).Name("ETMS code");
            Map(m => m.Province).Name("Province");
            Map(m => m.City).Name("City");
            Map(m => m.Hospital).Name("Hospital");
            Map(m => m.Department).Name("Department");
            Map(m => m.Email).Name("Email");
            Map(m => m.MobileNumber).Name("Phone");
            Map(m => m.RegisterDate).Name("Date");
        }
    }
    public class DateTimeConverter : DefaultTypeConverter
    {
        public override string ConvertToString(TypeConverterOptions options, object value)
        {
            if (value is DateTime)
            {
                return ((DateTime)value).ToString("yyyy/MM/dd/HH:mm:ss");
            }
            return value.ToString();
        }
    }

    public sealed class BizconfBehaviorDto
    {
        public string ViewerBu { get; set; }
        public string District { get; set; }
        public string Province { get; set; }
        public string City { get; set; }
        public string County { get; set; }
        public string Hospital { get; set; }
        public string Name { get; set; }
        public string Code { get; set; }
        public string Role { get; set; }
        public string Uid { get; set; }
        public string Email { get; set; }
        public string Mobile { get; set; }
        public DateTime JoinTime { get; set; }
        public DateTime LeaveTime { get; set; }
        public long ViewDuration { get; set; }
        public int? ViewerCount { get; set; }
        public int LoginCount { get; set; }
        public long LongestViewDuration { get; set; }
        public string ViewChannel { get; set; }
        public string MeetingBu { get; set; }
        public string MeetingName { get; set; }
        public string MeetingId { get; set; }
        public DateTime MeetingTime { get; set; }
        public string Product { get; set; }
        public string Project { get; set; }
        public string Field { get; set; }
        public string Comment { get; set; }
        public string LiveFlag { get; set; }
    }

    public sealed class BizconfBehaviorMap : CsvClassMap<BizconfBehaviorDto>
    {
        public BizconfBehaviorMap()
        {
            Map(m => m.ViewerBu).Name("观众所属BU");
            Map(m => m.District).Name("区域");
            Map(m => m.Province).Name("省");
            Map(m => m.City).Name("市");
            Map(m => m.County).Name("区/县");
            Map(m => m.Hospital).Name("医院");
            Map(m => m.Name).Name("姓名");
            Map(m => m.Code).Name("Code");
            Map(m => m.Role).Name("角色");
            Map(m => m.Uid).Name("UID");
            Map(m => m.Email).Name("邮箱");
            Map(m => m.Mobile).Name("手机号码");
            Map(m => m.JoinTime).Name("加入时间").TypeConverter<BizconfDateTimeConverter>();
            Map(m => m.LeaveTime).Name("离开时间").TypeConverter<BizconfDateTimeConverter>();
            Map(m => m.ViewDuration).Name("观看时长");
            Map(m => m.ViewerCount).Name("观看人数");
            Map(m => m.LoginCount).Name("登录次数");
            Map(m => m.LongestViewDuration).Name("最长一段观看时长");
            Map(m => m.ViewChannel).Name("观看渠道");
            Map(m => m.MeetingBu).Name("会议所属BU");
            Map(m => m.MeetingName).Name("会议名称");
            Map(m => m.MeetingId).Name("会议ID");
            Map(m => m.MeetingTime).Name("会议时间").TypeConverter<BizconfDateTimeConverter>();
            Map(m => m.Product).Name("产品");
            Map(m => m.Project).Name("项目");
            Map(m => m.Field).Name("治疗领域");
            Map(m => m.Comment).Name("会议提问");
            Map(m => m.LiveFlag).Name("直播/录播");
        }
    }

    public sealed class BizconfInteractionDto
    {
        public string MeetingBu { get; set; }
        public string MeetingName { get; set; }
        public string MeetingId { get; set; }
        public DateTime MeetingTime { get; set; }
        public string Product { get; set; }
        public string Project { get; set; }
        public string Field { get; set; }
        public string Comment { get; set; }
        public DateTime CommentTime { get; set; }
        public string Role { get; set; }
        public string Uid { get; set; }
    }

    public sealed class BizconfInteractionMap : CsvClassMap<BizconfInteractionDto>
    {
        public BizconfInteractionMap()
        {
            Map(m => m.MeetingBu).Name("会议所属BU");
            Map(m => m.MeetingName).Name("会议名称");
            Map(m => m.MeetingId).Name("会议ID");
            Map(m => m.MeetingTime).Name("会议时间").TypeConverter<BizconfDateTimeConverter>();
            Map(m => m.Product).Name("产品");
            Map(m => m.Project).Name("项目");
            Map(m => m.Field).Name("治疗领域");
            Map(m => m.Comment).Name("提问");
            Map(m => m.CommentTime).Name("提问时间").TypeConverter<BizconfDateTimeConverter>();
            Map(m => m.Role).Name("角色");
            Map(m => m.Uid).Name("UID");
        }
    }

    public sealed class BizconfMeetingDto
    {
        public string MeetingBu { get; set; }
        public string MeetingName { get; set; }
        public string MeetingId { get; set; }
        public DateTime MeetingTime { get; set; }
        public string Product { get; set; }
        public string Project { get; set; }
        public string Field { get; set; }
        public int ViewPoint { get; set; }
        public string Supplier { get; set; }
    }

    public sealed class BizconfMeetingMap : CsvClassMap<BizconfMeetingDto>
    {
        public BizconfMeetingMap()
        {
            Map(m => m.MeetingBu).Name("会议所属BU");
            Map(m => m.MeetingName).Name("会议名称");
            Map(m => m.MeetingId).Name("会议ID");
            Map(m => m.MeetingTime).Name("会议时间").TypeConverter<BizconfDateTimeConverter>();
            Map(m => m.Product).Name("产品");
            Map(m => m.Project).Name("项目");
            Map(m => m.Field).Name("治疗领域");
            Map(m => m.ViewPoint).Name("总观看点数");
            Map(m => m.Supplier).Name("视频供应商");
        }
    }
    public class BizconfDateTimeConverter : DefaultTypeConverter
    {
        public override string ConvertToString(TypeConverterOptions options, object value)
        {
            if (value is DateTime)
            {
                return ((DateTime)value).ToString("yyyy-MM-dd HH:mm:ss");
            }
            return value.ToString();
        }
    }

    public sealed class BizconfRawDto
    {
        public string Uid { get; set; }
        public string Role { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string Province { get; set; }
        public string City { get; set; }
        public string County { get; set; }
        public string Hospital { get; set; }
        public string Department { get; set; }
        public int ViewerCount { get; set; }
        public string ViewDuration { get; set; }
        public DateTime FirstLoginTime { get; set; }
        public DateTime LastOnlineTime { get; set; }
        public string LastLoginDevice { get; set; }
        public string Product { get; set; }
        public string Project { get; set; }
        public string Field { get; set; }
        public string MeetingBu { get; set; }
        public string MeetingId { get; set; }
        public string MeetingName { get; set; }
        public string LiveFlag { get; set; }
        public string Comment { get; set; }
    }

    public sealed class BizconfRawMap : CsvClassMap<CustomDbHelper.BizconfData>
    {
        public BizconfRawMap()
        {
            Map(m => m.Uid).Name("UID");
            Map(m => m.Role).Name("角色");
            Map(m => m.Name).Name("用户姓名");
            Map(m => m.Email).Name("邮箱");
            Map(m => m.Province).Name("医院省份");
            Map(m => m.City).Name("医院城市");
            Map(m => m.County).Name("医院区县");
            Map(m => m.Hospital).Name("医院名称");
            Map(m => m.Department).Name("医院科室");
            Map(m => m.ViewerCount).Name("观看人数");
            Map(m => m.ViewDuration).Name("直播观看时长");
            Map(m => m.FirstLoginTime).Name("首次登录时间").TypeConverter<BizconfDateTimeConverter>();
            Map(m => m.LastOnlineTime).Name("最后在线时间").TypeConverter<BizconfDateTimeConverter>();
            Map(m => m.LastLoginDevice).Name("最后登录设备");
            Map(m => m.Product).Name("产品");
            Map(m => m.Project).Name("项目");
            Map(m => m.Field).Name("治疗领域");
            Map(m => m.Bu).Name("会议所属BU");
            Map(m => m.MeetingId).Name("Meeting ID");
            Map(m => m.MeetingName).Name("会议名称");
            Map(m => m.LiveFlag).Name("直播标签");
            Map(m => m.Comment).Name("会议提问");
        }
    }
}