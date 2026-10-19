"""Generate reviewed synthetic SHP/DBF bytes on D; no ArcPy, SDK or GIS execution.

Fixtures intentionally include invalid geometry. Production inputs are read only.
Actual analysis is performed later via the existing registered MCP tools.
"""
import datetime as dt
import hashlib
import json
import pathlib
import struct
import sys
import uuid

base = pathlib.Path(r'D:\shiyanwenjaijia\自测记录\几何拓扑整改')
if base.resolve().drive.upper() != 'D:':
    raise ValueError('D root required')
run = base / (dt.datetime.now().strftime('%Y%m%d-%H%M%S') + '-' + uuid.uuid4().hex[:8])
run.mkdir(parents=True, exist_ok=False)
probe = run / '.write-probe'
probe.write_text('D')
assert probe.read_text() == 'D'
probe.unlink()
prj_source = pathlib.Path(r'D:\shiyanwenjaijia\输入资料\试题数据\试题数据\研究区道路.prj')
prj = prj_source.read_bytes()
fixtures = run / 'fixtures'
fixtures.mkdir()

def rectangle(x0, y0, x1, y1):
    return [(x0, y0), (x0, y1), (x1, y1), (x1, y0), (x0, y0)]

def write_shape(name, kind, shapes):
    # Each shape is a list of parts (each a sequence of XY); None is a null record.
    # Offset to a real projected coordinate range, preserving exact fixture dimensions.
    converted = [None if shape is None else [[(540000+x, 3468000+y) for x,y in part] for part in shape] for shape in shapes]
    all_points = [p for shape in converted if shape for part in shape for p in part]
    bbox = (min(p[0] for p in all_points), min(p[1] for p in all_points), max(p[0] for p in all_points), max(p[1] for p in all_points)) if all_points else (0, 0, 0, 0)
    contents = []
    for shape in converted:
        if shape is None:
            contents.append(struct.pack('<i', 0))
            continue
        points = [p for part in shape for p in part]
        bounds = (min(p[0] for p in points), min(p[1] for p in points), max(p[0] for p in points), max(p[1] for p in points))
        offsets = []
        offset = 0
        for part in shape:
            offsets.append(offset)
            offset += len(part)
        if kind == 8:
            content = struct.pack('<i4di', kind, *bounds, len(points))
        else:
            content = struct.pack('<i4d2i', kind, *bounds, len(shape), len(points)) + struct.pack('<' + 'i'*len(offsets), *offsets)
        content += b''.join(struct.pack('<dd', *p) for p in points)
        contents.append(content)
    def header(words):
        return struct.pack('>7i', 9994, 0, 0, 0, 0, 0, words) + struct.pack('<2i8d', 1000, kind, *bbox, 0, 0, 0, 0)
    shp = bytearray(header((100 + sum(8 + len(c) for c in contents)) // 2))
    shx = bytearray(header((100 + 8 * len(contents)) // 2))
    offset = 50
    for i, content in enumerate(contents, 1):
        shp += struct.pack('>ii', i, len(content)//2) + content
        shx += struct.pack('>ii', offset, len(content)//2)
        offset += 4 + len(content)//2
    path = fixtures / (name + '.shp')
    path.write_bytes(shp)
    path.with_suffix('.shx').write_bytes(shx)
    # One ID attribute; intentionally no cleanup or geometry repair.
    today = dt.date.today()
    dbf = bytearray(struct.pack('<4BIHH20x', 3, today.year-1900, today.month, today.day, len(shapes), 65, 11))
    dbf += struct.pack('<11sc4xBB14x', b'CASE_ID', b'N', 10, 0) + b'\r'
    for i in range(len(shapes)):
        dbf += b' ' + str(i).rjust(10).encode('ascii')
    dbf += b'\x1a'
    path.with_suffix('.dbf').write_bytes(dbf)
    path.with_suffix('.prj').write_bytes(prj)
    path.with_suffix('.cpg').write_bytes(b'UTF-8')
    return str(path)

paths = {}
paths['adjacent'] = write_shape('adjacent', 5, [[rectangle(0,0,10,10)], [rectangle(10,0,20,10)]])
paths['overlap'] = write_shape('overlap', 5, [[rectangle(0,0,10,10)], [rectangle(5,0,15,10)]])
paths['containment'] = write_shape('containment', 5, [[rectangle(0,0,20,20)], [rectangle(5,5,10,10)]])
paths['duplicate_polygons'] = write_shape('duplicate_polygons', 5, [[rectangle(0,0,10,10)], [rectangle(0,0,10,10)]])
paths['gap'] = write_shape('gap', 5, [[rectangle(0,0,30,10)], [rectangle(0,10,10,20)], [rectangle(20,10,30,20)], [rectangle(0,20,30,30)]])
paths['donut'] = write_shape('donut', 5, [[rectangle(0,0,30,30), list(reversed(rectangle(10,10,20,20)))]])
paths['bowtie'] = write_shape('bowtie', 5, [[[(0,0),(10,10),(0,10),(10,0),(0,0)]]])
paths['duplicate_vertex'] = write_shape('duplicate_vertex', 5, [[[(0,0),(0,10),(0,10),(10,10),(10,0),(0,0)]]])
paths['null_geometry'] = write_shape('null_geometry', 5, [[rectangle(0,0,10,10)], None])
paths['short_segment'] = write_shape('short_segment', 3, [[[(0,0),(0.001,0),(10,0)]]])
paths['self_overlap'] = write_shape('self_overlap', 3, [[[(0,0),(10,0),(5,0)]]])
paths['self_touch'] = write_shape('self_touch', 3, [[[(0,0),(10,0),(10,10),(0,10),(0,0),(-10,0)]]])
paths['self_cross'] = write_shape('self_cross', 3, [[[(0,0),(10,10),(0,10),(10,0)]]])
paths['vertex_cross'] = write_shape('vertex_cross', 3, [[[(-10,-10),(0,0),(10,10),(10,-10),(0,0),(-10,10)]]])
paths['vertex_segment_cross'] = write_shape('vertex_segment_cross', 3, [[[(-10,-10),(0,0),(10,10),(10,0),(-10,0)]]])
paths['duplicate_node_cross'] = write_shape('duplicate_node_cross', 3, [[[(-10,-10),(0,0),(0,0),(10,10),(10,-10),(0,0),(0,0),(-10,10)]]])
paths['crossing_lines'] = write_shape('crossing_lines', 3, [[[(0,0),(10,10)]], [[(0,10),(10,0)]]])
paths['closed_line'] = write_shape('closed_line', 3, [[rectangle(0,0,10,10)]])
paths['open_lines'] = write_shape('open_lines', 3, [[[(0,0),(10,0)]], [[(10,0),(20,0)]]])
paths['near_endpoints'] = write_shape('near_endpoints', 3, [[[(0,0),(10,0)]], [[(10.05,0),(20,0)]]])
paths['multipart'] = write_shape('multipart', 5, [[rectangle(0,0,10,10), rectangle(20,0,30,10)]])
paths['multipoint'] = write_shape('multipoint', 8, [[[(0,0),(0,0),(10,0)]]])
paths['empty'] = write_shape('empty', 5, [])
# Real workload to leave time to observe/capture a GP callback without artificial sleeps in GIS.
paths['gp_stress_input'] = write_shape('gp_stress_input', 3, [[[(x*20,y*20),(x*20+15,y*20+10)]] for x in range(100) for y in range(100)])
manifest = [{'path': str(p), 'bytes': p.stat().st_size, 'sha256': hashlib.sha256(p.read_bytes()).hexdigest().upper()} for p in sorted(fixtures.iterdir())]
(run/'fixtures.json').write_text(json.dumps({'origin':'SYNTHETIC_TEST_DATA_NOT_USER_PRODUCTION','paths':paths,'manifest':manifest,'sourcePrj':str(prj_source)}, ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'run':str(run),'fixtures':len(paths),'files':len(manifest)},ensure_ascii=False))
