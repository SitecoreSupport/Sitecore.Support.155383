// --------------------------------------------------------------------------------------------------------------------
// <copyright file="SitecoreItemCrawler.cs" company="Sitecore">
//   Copyright (c) Sitecore. All rights reserved.
// </copyright>
// <summary>
//   The sitecore item crawler.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using Sitecore.Configuration;
using Sitecore.Data.LanguageFallback;
using Sitecore.Globalization;

namespace Sitecore.Support.ContentSearch
{
  using System;
  using System.Collections.Generic;
  using System.Globalization;
  using System.Linq;
  using System.Threading;

  using Sitecore.Collections;
  using Sitecore.Abstractions;

  using Sitecore.ContentSearch;
  using Sitecore.ContentSearch.Diagnostics;
  using Sitecore.ContentSearch.Pipelines.GetContextIndex;
  using Sitecore.ContentSearch.Utilities;
  using Sitecore.Data;
  using Sitecore.Data.Items;
  using Sitecore.Data.Managers;
  using Sitecore.Diagnostics;
  using Sitecore.Events;
  using Sitecore.SecurityModel;

  /// <summary>
  /// The sitecore item crawler.
  /// </summary>
  public class SitecoreItemCrawler : Sitecore.ContentSearch.SitecoreItemCrawler
  {
    /// <summary>
    /// Executes the update event.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="indexable">The indexable.</param>
    /// <param name="operationContext">The operation context.</param>
    protected override void DoUpdate(IProviderUpdateContext context, SitecoreIndexableItem indexable, IndexEntryOperationContext operationContext)
    {
      Assert.ArgumentNotNull(context, "context");
      Assert.ArgumentNotNull(indexable, "indexable");

      using (new LanguageFallbackItemSwitcher(this.Index.EnableItemLanguageFallback))
      {
        if (this.IndexUpdateNeedDelete(indexable))
        {
          this.Index.Locator.GetInstance<IEvent>().RaiseEvent("indexing:deleteitem", this.index.Name, indexable.UniqueId, indexable.AbsolutePath);
          this.Operations.Delete(indexable, context);
          return;
        }

        this.Index.Locator.GetInstance<IEvent>().RaiseEvent("indexing:updatingitem", this.index.Name, indexable.UniqueId, indexable.AbsolutePath);
        if (!this.IsExcludedFromIndex(indexable, true))
        {
          /*************************************************************/
          if (operationContext != null && !operationContext.NeedUpdateAllVersions)
          {
            this.UpdateItemVersion(context, indexable, operationContext);
          }
          else
          {
            var languages = (operationContext != null && !operationContext.NeedUpdateAllLanguages) ? new[] { indexable.Item.Language } : indexable.Item.Languages;


            foreach (var language in languages)
            {
              Item latestVersion;
              // Sitecore.Support.155383 patch. Switched from SitecoreCachesDisabler() to WriteCachesDisabler()
              using (new WriteCachesDisabler())
              {
                latestVersion = indexable.Item.Database.GetItem(indexable.Item.ID, language, Data.Version.Latest);
              }

              if (latestVersion == null)
              {
                CrawlingLog.Log.Warn(string.Format("SitecoreItemCrawler : Update : Latest version not found for item {0}. Skipping.", indexable.Item.Uri));
                continue;
              }

              Item[] versions;
              using (new WriteCachesDisabler())
              {
                versions = latestVersion.Versions.GetVersions(false);
              }

              foreach (var version in versions)
              {
                this.UpdateItemVersion(context, version, operationContext);
              }
            }
          }

          /*************************************************************/
          this.Index.Locator.GetInstance<IEvent>().RaiseEvent("indexing:updateditem", this.index.Name, indexable.UniqueId, indexable.AbsolutePath);
        }

        if (this.DocumentOptions.ProcessDependencies)
        {
          this.Index.Locator.GetInstance<IEvent>().RaiseEvent("indexing:updatedependents", this.index.Name, indexable.UniqueId, indexable.AbsolutePath);
          this.UpdateDependents(context, indexable);
        }
      }
    }
  }
}
