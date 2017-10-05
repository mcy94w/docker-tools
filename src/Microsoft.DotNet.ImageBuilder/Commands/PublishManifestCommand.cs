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
using System.Threading.Tasks;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class PublishManifestCommand : Command<PublishManifestOptions>
    {
        public PublishManifestCommand() : base()
        {
        }

        public override Task ExecuteAsync()
        {
            Utilities.WriteHeading("GENERATING MANIFESTS");
            IEnumerable<ImageInfo> multiArchImages = Manifest.Repos
                .SelectMany(repo => repo.Images)
                .Where(image => image.SharedTags.Any());
            foreach (ImageInfo image in multiArchImages)
            {
                IEnumerable<IGrouping<string, TagInfo>> osTagGroupings = image.SharedTags
                    .GroupBy(sharedTag => ((SharedTag)sharedTag.Model).OS);
                foreach (IGrouping<string, TagInfo> osTagGrouping in osTagGroupings)
                {
                    string qualifiedImageName = osTagGrouping.First().FullyQualifiedName;
                    IEnumerable<string> additionalTags = osTagGrouping
                        .Select(tag => tag.Name)
                        .Skip(1);
                    IEnumerable<PlatformInfo> activeOsPlatforms = image.Platforms
                        .Where(platform => string.IsNullOrEmpty(osTagGrouping.Key)
                            || string.Equals(platform.Model.OS, osTagGrouping.Key, StringComparison.OrdinalIgnoreCase));
                    string manifestYml = GenerateManifestYml(qualifiedImageName, additionalTags, activeOsPlatforms);

                    Console.WriteLine($"-- PUBLISHING MANIFEST:{Environment.NewLine}{manifestYml}");
                    File.WriteAllText("manifest.yml", manifestYml);

                    // ExecuteWithRetry because the manifest-tool fails periodically with communicating
                    // with the Docker Registry.
                    ExecuteHelper.ExecuteWithRetry(
                        "manifest-tool",
                        $"--username {Options.Username} --password {Options.Password} push from-spec manifest.yml",
                        Options.IsDryRun);
                }
            }

            return Task.CompletedTask;
        }

        private string GenerateManifestYml(string image, IEnumerable<string> tags, IEnumerable<PlatformInfo> platforms)
        {
            StringBuilder manifestYml = new StringBuilder();
            manifestYml.AppendLine($"image: {image}");

            if (tags.Any())
            {
                manifestYml.AppendLine($"tags: [{string.Join(",", tags)}]");
            }

            manifestYml.AppendLine("manifests:");
            foreach (PlatformInfo platform in platforms)
            {
                manifestYml.AppendLine($"  -");
                manifestYml.AppendLine($"    image: {platform.Tags.First().FullyQualifiedName}");
                manifestYml.AppendLine($"    platform:");
                manifestYml.AppendLine($"      architecture: {platform.Model.Architecture.ToString().ToLowerInvariant()}");
                manifestYml.AppendLine($"      os: {platform.Model.OS}");
                if (platform.Model.Variant != null)
                {
                    manifestYml.AppendLine($"      variant: {platform.Model.Variant}");
                }
            }

            return manifestYml.ToString();
        }
    }
}
