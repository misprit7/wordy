﻿using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

WordprocessingDocument doc = WordprocessingDocument.Open("./rtl-test.docx", true);

Body body = doc.MainDocumentPart.Document.Body;


