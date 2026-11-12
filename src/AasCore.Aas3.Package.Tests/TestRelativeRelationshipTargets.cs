// SPDX-FileCopyrightText: 2026 aas-package3-csharp AUTHORS
//
// SPDX-License-Identifier: MIT

using System.Collections.Generic; // can't alias
using System.Linq; // can't alias
using NUnit.Framework; // can't alias
using MemoryStream = System.IO.MemoryStream;
using StreamWriter = System.IO.StreamWriter;
using Uri = System.Uri;
using UTF8Encoding = System.Text.UTF8Encoding;
using UriKind = System.UriKind;
using ZipArchive = System.IO.Compression.ZipArchive;
using ZipArchiveMode = System.IO.Compression.ZipArchiveMode;

namespace AasCore.Aas3.Package.Tests
{
    /**
     * <summary>
     * Test packages whose relationships use relative targets, which are valid
     * according to ISO/IEC 29500-2 and IDTA-01005.
     * </summary>
     */
    public class TestRelativeRelationshipTargets
    {
        private const string RelsHeader =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">";

        private const string RelsFooter = "</Relationships>";

        /**
         * <summary>
         * Write a package as a raw ZIP archive so that the targets in the
         * relationship parts are exactly as written here.
         * </summary>
         */
        private static MemoryStream PackageWithRelativeTargets()
        {
            var entries = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>(
                    "[Content_Types].xml",
                    "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                    "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                    "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\" />" +
                    "<Default Extension=\"xml\" ContentType=\"text/xml\" />" +
                    "<Default Extension=\"png\" ContentType=\"image/png\" />" +
                    "<Override PartName=\"/aasx/aasx-origin\" ContentType=\"text/plain\" />" +
                    "<Override PartName=\"/aasx/documentation/abc/manual_pdf\" ContentType=\"application/pdf\" />" +
                    "</Types>"),
                new KeyValuePair<string, string>(
                    "_rels/.rels",
                    RelsHeader +
                    "<Relationship Type=\"http://admin-shell.io/aasx/relationships/aasx-origin\" " +
                    "Target=\"aasx/aasx-origin\" Id=\"R1\" />" +
                    "<Relationship Type=\"http://schemas.openxmlformats.org/package/2006/relationships/metadata/thumbnail\" " +
                    "Target=\"No_image.png\" Id=\"R2\" />" +
                    RelsFooter),
                new KeyValuePair<string, string>(
                    "aasx/_rels/aasx-origin.rels",
                    RelsHeader +
                    "<Relationship Type=\"http://admin-shell.io/aasx/relationships/aas-spec\" " +
                    "Target=\"xml/content.xml\" Id=\"R3\" />" +
                    RelsFooter),
                new KeyValuePair<string, string>(
                    "aasx/xml/_rels/content.xml.rels",
                    RelsHeader +
                    "<Relationship Type=\"http://admin-shell.io/aasx/relationships/aas-suppl\" " +
                    "Target=\"../documentation/abc/manual_pdf\" Id=\"R4\" />" +
                    RelsFooter),
                new KeyValuePair<string, string>(
                    "aasx/aasx-origin", "Intentionally empty"),
                new KeyValuePair<string, string>(
                    "aasx/xml/content.xml", "<environment />"),
                new KeyValuePair<string, string>(
                    "aasx/documentation/abc/manual_pdf", "some manual"),
                new KeyValuePair<string, string>(
                    "No_image.png", "some image")
            };

            var stream = new MemoryStream();
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
            {
                foreach (var entry in entries)
                {
                    using var writer = new StreamWriter(
                        archive.CreateEntry(entry.Key).Open(), new UTF8Encoding(false));
                    writer.Write(entry.Value);
                }
            }

            stream.Position = 0;
            return stream;
        }

        [Test]
        public void Test_reading_a_package_with_relative_targets()
        {
            using var stream = PackageWithRelativeTargets();

            var packaging = new Packaging();
            using var pkgOrErr = packaging.OpenRead(stream);
            var pkg = pkgOrErr.Must();

            var specs = pkg.Specs().ToList();
            Assert.That(specs.Count, Is.EqualTo(1));
            Assert.That(specs[0].Uri.ToString(), Is.EqualTo("/aasx/xml/content.xml"));
            Assert.That(specs[0].ReadAllText(), Is.EqualTo("<environment />"));
            Assert.That(pkg.IsSpec(specs[0]), Is.True);

            var suppls = pkg.SupplementariesFor(specs[0]).ToList();
            Assert.That(suppls.Count, Is.EqualTo(1));
            Assert.That(
                suppls[0].Uri.ToString(),
                Is.EqualTo("/aasx/documentation/abc/manual_pdf"));
            Assert.That(suppls[0].ReadAllText(), Is.EqualTo("some manual"));

            var supplRels = pkg.SupplementaryRelationships().ToList();
            Assert.That(supplRels.Count, Is.EqualTo(1));
            Assert.That(
                supplRels[0].Supplementary.Uri.ToString(),
                Is.EqualTo("/aasx/documentation/abc/manual_pdf"));

            var thumbnail = pkg.Thumbnail();
            Assert.That(thumbnail, Is.Not.Null);
            Assert.That(thumbnail!.Uri.ToString(), Is.EqualTo("/No_image.png"));
            Assert.That(thumbnail.ReadAllText(), Is.EqualTo("some image"));
        }

        [Test]
        public void Test_modifying_relationships_with_relative_targets()
        {
            using var stream = PackageWithRelativeTargets();

            var packaging = new Packaging();

            {
                using var pkgOrErr = packaging.OpenReadWrite(stream);
                var pkg = pkgOrErr.Must();

                var spec = pkg.Specs().Single();
                var suppl = pkg.MustPart(
                    new Uri("/aasx/documentation/abc/manual_pdf", UriKind.Relative));

                // The relationship already exists with a relative target,
                // so no duplicate must be created.
                pkg.RelateSupplementaryToSpec(suppl, spec);
                Assert.That(pkg.SupplementariesFor(spec).Count(), Is.EqualTo(1));

                pkg.UnrelateSupplementaryFromSpec(suppl, spec);
                Assert.That(pkg.SupplementariesFor(spec).Count(), Is.EqualTo(0));

                pkg.UnmakeSpec(spec);
                Assert.That(pkg.Specs().Count(), Is.EqualTo(0));
                Assert.That(pkg.IsSpec(spec), Is.False);

                pkg.Flush();
            }

            {
                stream.Position = 0;
                using var pkgOrErr = packaging.OpenRead(stream);
                var pkg = pkgOrErr.Must();

                Assert.That(pkg.Specs().Count(), Is.EqualTo(0));
                Assert.That(pkg.SupplementaryRelationships().Count(), Is.EqualTo(0));
            }
        }
    }
}
