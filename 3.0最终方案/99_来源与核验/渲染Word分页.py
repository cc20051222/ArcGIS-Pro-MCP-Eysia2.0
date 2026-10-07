from pathlib import Path
import pypdfium2 as pdfium
import hashlib,json

BASE=Path(r'D:\ArcGIS-Pro-MCP 2.0\3.0最终方案')
QA=BASE/'99_来源与核验'/'Word排版'
PDF=QA/'项目介绍原生分页.pdf'
OUT=QA/'rendered'
OUT.mkdir(exist_ok=True)
docx=next((BASE/'06_Word项目介绍').glob('*.docx'))
sha=lambda p: hashlib.sha256(p.read_bytes()).hexdigest().upper()
prior={x.name:sha(x) for x in OUT.glob('page-*.png')}
doc=pdfium.PdfDocument(str(PDF))
changed,unchanged,records=[],[],[]
for i in range(len(doc)):
    dest=OUT/f'page-{i+1}.png'
    doc[i].render(scale=2).to_pil().save(dest)
    h=sha(dest)
    (unchanged if prior.get(dest.name)==h else changed).append(i+1)
    records.append({'page':i+1,'sha256':h,'path':str(dest)})
for image in OUT.glob('page-*.png'):
    if int(image.stem.split('-')[-1])>len(doc):
        assert image.resolve().parent==OUT.resolve()
        image.unlink()
result={'renderer':'Microsoft Word native PDF export then PDFium rasterization','docxSha256':sha(docx),'pdfSha256':sha(PDF),
        'pages':len(doc),'changedPages':changed,'unchangedPages':unchanged,'images':records,'scale':2,'dpi':144}
(QA/'最终页面指纹.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({k:result[k] for k in ['docxSha256','pages','changedPages','unchangedPages']},ensure_ascii=False))
