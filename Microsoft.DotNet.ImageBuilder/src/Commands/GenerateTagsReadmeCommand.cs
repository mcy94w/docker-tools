// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.Model;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class GenerateTagsReadmeCommand : Command<GenerateTagsReadmeOptions>
    {
        public GenerateTagsReadmeCommand() : base()
        {
        }

        public override Task ExecuteAsync()
        {
            Logger.WriteHeading("GENERATING TAGS README");
            foreach (RepoInfo repo in Manifest.Repos)
            {
                string tagsDoc = GetTagsDocumentation(repo);

                Logger.WriteSubheading($"{repo.Name} Tags Documentation:");
                Logger.WriteMessage();
                Logger.WriteMessage(tagsDoc);

                if (Options.UpdateReadme)
                {
                    UpdateReadme(tagsDoc, repo);
                }
            }

            return Task.CompletedTask;
        }

        private static string GetArchitectureDisplayName(Architecture architecture)
        {
            string displayName;

            switch (architecture)
            {
                case Architecture.ARM:
                    displayName = "arm32";
                    break;
                default:
                    displayName = architecture.ToString().ToLowerInvariant();
                    break;
            }

            return displayName;
        }

        private string GetTagsDocumentation(RepoInfo repo)
        {
            StringBuilder tagsDoc = new StringBuilder();

            var platformGroups = GetOrderedImagePlatforms(repo)
                .GroupBy(tuple => new { tuple.Platform.Model.OS, tuple.Platform.Model.OsVersion, tuple.Platform.Model.Architecture })
                .OrderByDescending(platformGroup => platformGroup.Key.Architecture)
                .ThenBy(platformGroup => platformGroup.Key.OS)
                .ThenByDescending(platformGroup => platformGroup.Key.OsVersion);

            foreach (var platformGroup in platformGroups)
            {
                string os = GetOsDisplayName(platformGroup.Key.OS, platformGroup.Key.OsVersion);
                string arch = GetArchitectureDisplayName(platformGroup.Key.Architecture);
                tagsDoc.AppendLine($"# Supported {os} {arch} tags");
                tagsDoc.AppendLine();

                foreach (var tuple in platformGroup)
                {
                    IEnumerable<string> documentedTags = GetDocumentedTags(tuple.Platform.Tags)
                        .Concat(GetDocumentedTags(tuple.Image.SharedTags));

                    if (!documentedTags.Any())
                    {
                        continue;
                    }

                    string tags = documentedTags
                        .Select(tag => $"`{tag}`")
                        .Aggregate((working, next) => $"{working}, {next}");
                    string dockerfile = tuple.Platform.DockerfilePath.Replace('\\', '/');
                    tagsDoc.AppendLine($"- [{tags} (*{dockerfile}*)]({Options.SourceUrl}/{dockerfile})");
                }

                tagsDoc.AppendLine();
            }

            return tagsDoc.ToString();
        }

        private static IEnumerable<string> GetDocumentedTags(IEnumerable<TagInfo> tagInfos)
        {
            return tagInfos.Where(tag => !tag.Model.IsUndocumented)
                .Select(tag => tag.Name);
        }

        private IEnumerable<ImagePlatformTuple> GetOrderedImagePlatforms(RepoInfo repo)
        {
            IEnumerable<ImagePlatformTuple> tuples = repo.Images
                .SelectMany(image => image.Platforms.Select(platform => new ImagePlatformTuple(image, platform)));

            switch (Options.Order)
            {
                case TagSortOrder.Manifest:
                    if (repo.Model.ReadmeOrder != null)
                    {
                        tuples = SortTagsByManifestOrder(tuples, repo);
                    }
                    // else - use the order the images appear in the manifest
                    break;
                case TagSortOrder.DescendingVersionAscendingVariant:
                    tuples = tuples
                        .OrderByDescending(tuple => TagInfo.GetTagVersion(tuple.Platform.Tags.First().Name))
                        .ThenBy(tuple => TagInfo.GetTagVariant(tuple.Platform.Tags.First().Name));
                    break;
                default:
                    throw new NotSupportedException();
            }

            return tuples;
        }

        private static string GetOsDisplayName(OS os, string osVersion)
        {
            string displayName;

            switch (os)
            {
                case OS.Windows:
                    if (osVersion != null && (osVersion.Contains("1709") || osVersion.Contains("16299")))
                    {
                        displayName = "Windows Server, version 1709";
                    }
                    else
                    {
                        displayName = "Windows Server 2016";
                    }
                    break;
                default:
                    displayName = os.ToString();
                    break;
            }

            return displayName;
        }

        private static string NormalizeLineEndings(string value, string targetFormat)
        {
            string targetLineEnding = targetFormat.Contains("\r\n") ? "\r\n" : "\n";
            if (targetLineEnding != Environment.NewLine)
            {
                value = value.Replace(Environment.NewLine, targetLineEnding);
            }

            return value;
        }

        private IEnumerable<ImagePlatformTuple> SortTagsByManifestOrder(IEnumerable<ImagePlatformTuple> tags, RepoInfo repo)
        {
            // TODO:  Exceptions
            var sortedTags = new List<ImagePlatformTuple>();
            foreach (string tag in repo.Model.ReadmeOrder)
            {
                sortedTags.Add(tags.Single(tuple => tuple.Platform.Tags.Where(ti => ti.Name == tag).Any()));
            }

            return sortedTags;
        }

        private static void UpdateReadme(string tagsDocumentation, RepoInfo repo)
        {
            Logger.WriteHeading("UPDATING README");

            string readme = File.ReadAllText(repo.Model.ReadmePath);

            // tagsDocumentation is formatted with Environment.NewLine which may not match the readme format. This can
            // happen when image-builder is invoked within a Linux container on a Windows host while using a host volume.
            // Normalize the line endings to match the readme.
            tagsDocumentation = NormalizeLineEndings(tagsDocumentation, readme);

            string updatedReadme = Regex.Replace(readme, "(# .*\\s*(- \\[.*\\s*)+)+", tagsDocumentation);
            File.WriteAllText(repo.Model.ReadmePath, updatedReadme);

            Logger.WriteSubheading($"Updated '{repo.Model.ReadmePath}'");
            Logger.WriteMessage();
        }

        private class ImagePlatformTuple
        {
            public ImageInfo Image { get; }
            public PlatformInfo Platform { get; }

            public ImagePlatformTuple(ImageInfo image, PlatformInfo platform)
            {
                Image = image;
                Platform = platform;
            }
        }
    }
}
