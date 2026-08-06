using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using HomeMind.Business.IServices.Expert;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace HomeMind.Business.Services.Expert;

/// <summary>基于 OpenXML SDK 的最小 PPTX 生成器：16:9 版式，标题页 + 逐页标题/要点文本框。</summary>
public sealed class OpenXmlPptxBuilder : IPptxBuilder
{
    private const int SlideWidth = 12192000; // 13.333 in，16:9
    private const int SlideHeight = 6858000; // 7.5 in
    private const int MaxBulletsPerSlide = 6;

    public byte[] Build(string title, string subtitle, IReadOnlyList<PptSlide> slides)
    {
        using var stream = new MemoryStream();
        using (var document = PresentationDocument.Create(stream, PresentationDocumentType.Presentation))
        {
            var presentationPart = document.AddPresentationPart();
            presentationPart.Presentation = new Presentation
            {
                SlideSize = new SlideSize { Cx = SlideWidth, Cy = SlideHeight, Type = SlideSizeValues.Screen16x9 },
                NotesSize = new NotesSize { Cx = 6858000, Cy = 9144000 },
                SlideMasterIdList = new SlideMasterIdList(),
                SlideIdList = new SlideIdList()
            };

            // 最小 SlideMaster + SlideLayout 结构（PowerPoint/WPS 均可打开）。
            var masterPart = presentationPart.AddNewPart<SlideMasterPart>();
            var layoutPart = masterPart.AddNewPart<SlideLayoutPart>();
            layoutPart.SlideLayout = BuildLayout();
            layoutPart.SlideLayout.Save();
            masterPart.SlideMaster = BuildMaster();
            masterPart.SlideMaster.Append(new SlideLayoutIdList(new SlideLayoutId { RelationshipId = masterPart.GetIdOfPart(layoutPart) }));
            masterPart.SlideMaster.Save();
            presentationPart.Presentation.SlideMasterIdList!.Append(new SlideMasterId
            {
                Id = 2147483648U,
                RelationshipId = presentationPart.GetIdOfPart(masterPart)
            });

            uint slideId = 256;
            AppendContentSlide(presentationPart, layoutPart, slideId++, title, subtitle, true);
            foreach (var slide in slides)
            {
                AppendContentSlide(presentationPart, layoutPart, slideId++, slide.Title, null, false, slide.Bullets);
            }
            presentationPart.Presentation.Save();
        }
        return stream.ToArray();
    }

    private static SlideMaster BuildMaster()
    {
        var master = new SlideMaster();
        master.AddNamespaceDeclaration("a", "http://schemas.openxmlformats.org/drawingml/2006/main");
        master.AddNamespaceDeclaration("r", "http://schemas.openxmlformats.org/officeDocument/2006/relationships");
        master.Append(
            new CommonSlideData(
                new ShapeTree(
                    BuildGroupShapeProperties(),
                    new P.Shape(
                        new NonVisualShapeProperties(
                            new NonVisualDrawingProperties { Id = 1, Name = "" },
                            new NonVisualShapeDrawingProperties()),
                        new ShapeProperties(),
                        new TextBody()))));
        return master;
    }

    private static SlideLayout BuildLayout()
    {
        var layout = new SlideLayout { Type = SlideLayoutValues.Blank };
        layout.AddNamespaceDeclaration("a", "http://schemas.openxmlformats.org/drawingml/2006/main");
        layout.AddNamespaceDeclaration("r", "http://schemas.openxmlformats.org/officeDocument/2006/relationships");
        layout.Append(new CommonSlideData(new ShapeTree(BuildGroupShapeProperties(), new P.Shape(
            new NonVisualShapeProperties(
                new NonVisualDrawingProperties { Id = 1, Name = "" },
                new NonVisualShapeDrawingProperties()),
            new ShapeProperties(),
            new TextBody()))));
        return layout;
    }

    private static void AppendContentSlide(PresentationPart presentationPart, SlideLayoutPart layoutPart, uint id, string title, string? subtitle, bool isTitleSlide, IReadOnlyList<string>? bullets = null)
    {
        var slidePart = presentationPart.AddNewPart<SlidePart>();
        slidePart.Slide = BuildSlide(title, subtitle, isTitleSlide, bullets);
        slidePart.Slide.Save();
        slidePart.AddPart(layoutPart);
        presentationPart.Presentation.SlideIdList!.Append(new SlideId { Id = id, RelationshipId = presentationPart.GetIdOfPart(slidePart) });
    }

    private static Slide BuildSlide(string title, string? subtitle, bool isTitleSlide, IReadOnlyList<string>? bullets)
    {
        var shapeTree = new ShapeTree(BuildGroupShapeProperties());
        if (isTitleSlide)
        {
            shapeTree.Append(BuildTextShape("Title", 2, PlaceholderValues.Title, title, 0, 0, SlideWidth, SlideHeight / 2, 36, bold: true, centered: true));
            if (!string.IsNullOrWhiteSpace(subtitle))
                shapeTree.Append(BuildTextShape("Subtitle", 3, PlaceholderValues.SubTitle, subtitle!, 0, SlideHeight / 2, SlideWidth, SlideHeight / 2, 20, centered: true));
        }
        else
        {
            shapeTree.Append(BuildTextShape("SlideTitle", 2, PlaceholderValues.Title, title, 457200, 228600, SlideWidth - 914400, 914400, 28, bold: true));
            shapeTree.Append(BuildTextShape("Content", 3, PlaceholderValues.Body, string.Join('\n', (bullets ?? Array.Empty<string>()).Take(MaxBulletsPerSlide)), 685800, 1371600, SlideWidth - 1371600, SlideHeight - 1600200, 18));
        }
        var slide = new Slide();
        slide.AddNamespaceDeclaration("a", "http://schemas.openxmlformats.org/drawingml/2006/main");
        slide.AddNamespaceDeclaration("r", "http://schemas.openxmlformats.org/officeDocument/2006/relationships");
        slide.Append(new CommonSlideData(shapeTree));
        return slide;
    }

    private static P.Shape BuildTextShape(string name, uint id, PlaceholderValues placeholder, string text, long x, long y, long cx, long cy, int fontSizePt, bool bold = false, bool centered = false)
    {
        var lines = text.Split('\n');
        var paragraphElements = lines.Select(line =>
        {
            var paragraph = new A.Paragraph(
                new A.ParagraphProperties { Alignment = centered ? A.TextAlignmentTypeValues.Center : A.TextAlignmentTypeValues.Left },
                new A.Run(
                    new A.RunProperties { FontSize = fontSizePt * 100, Bold = bold, Language = "zh-CN" },
                    new A.Text(line)));
            return paragraph;
        }).ToList();
        return new P.Shape(
            new NonVisualShapeProperties(
                new NonVisualDrawingProperties { Id = id, Name = name },
                new NonVisualShapeDrawingProperties()),
            new ShapeProperties(
                new A.Transform2D(new A.Offset { X = x, Y = y }, new A.Extents { Cx = cx, Cy = cy }),
                new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle },
                new A.SolidFill(new A.SchemeColor { Val = A.SchemeColorValues.Background1 })),
            BuildTextBody(paragraphElements));
    }

    private static TextBody BuildTextBody(IReadOnlyList<A.Paragraph> paragraphs)
    {
        var body = new TextBody(new A.BodyProperties { Anchor = A.TextAnchoringTypeValues.Top }, new A.ListStyle());
        foreach (var paragraph in paragraphs) body.Append(paragraph);
        return body;
    }

    private static P.GroupShapeProperties BuildGroupShapeProperties()
    {
        var groupShapeProperties = new P.GroupShapeProperties();
        groupShapeProperties.AddNamespaceDeclaration("a", "http://schemas.openxmlformats.org/drawingml/2006/main");
        groupShapeProperties.Append(new A.TransformGroup
        {
            Offset = new A.Offset { X = 0, Y = 0 },
            Extents = new A.Extents { Cx = 0, Cy = 0 },
            ChildOffset = new A.ChildOffset { X = 0, Y = 0 },
            ChildExtents = new A.ChildExtents { Cx = 0, Cy = 0 }
        });
        return groupShapeProperties;
    }
}
