#!/usr/bin/env python3
"""Read WorldMesh GLB and optional 3DGS PLY; emit explicit Unity-space assets.

No generation APIs, GPU training, or automatic object/GS segmentation are invoked.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import re
import struct
import numpy as np
import trimesh

AXES = {'y': np.diag([1., 1., -1., 1.]),
        'z': np.array([[1.,0,0,0],[0,0,1.,0],[0,1.,0,0],[0,0,0,1.]])}
PLY_TYPES = {'float':'f4','float32':'f4','double':'f8','float64':'f8','uchar':'u1','uint8':'u1','char':'i1','int8':'i1','short':'i2','ushort':'u2','int':'i4','uint':'u4'}

def safe_name(value):
    return re.sub(r'[^a-zA-Z0-9_-]+','_',str(value)).strip('_')[:64] or 'object'

def sha256(path):
    h=hashlib.sha256()
    with open(path,'rb') as stream:
        for block in iter(lambda:stream.read(1024*1024),b''):h.update(block)
    return h.hexdigest()

def read_ply(path):
    with open(path,'rb') as f:
        if f.readline().strip()!=b'ply':raise ValueError('Not a PLY file')
        fmt=None;count=0;props=[];element=None
        for _ in range(2048):
            line=f.readline().decode('ascii').strip()
            if line=='end_header':break
            parts=line.split()
            if not parts:continue
            if parts[0]=='format':fmt=parts[1]
            if parts[0]=='element':
                element=parts[1]
                if element=='vertex':count=int(parts[2])
                elif not count:raise ValueError('Vertex must be first PLY element')
            if parts[0]=='property' and element=='vertex':
                if parts[1]=='list' or parts[1] not in PLY_TYPES:raise ValueError('Unsupported PLY vertex property')
                props.append((parts[2],PLY_TYPES[parts[1]]))
        else:raise ValueError('PLY header too large')
        if count<=0 or count>50_000_000:raise ValueError('Invalid PLY count')
        if fmt=='binary_little_endian':
            dtype=np.dtype([(n,'<'+t) for n,t in props]);result=np.fromfile(f,dtype=dtype,count=count)
            if len(result)!=count:raise ValueError('Truncated PLY')
        elif fmt=='ascii':
            rows=[]
            for _ in range(count):
                values=f.readline().split()
                if len(values)!=len(props):raise ValueError('Invalid ASCII PLY vertex')
                rows.append(tuple(float(v) for v in values))
            result=np.array(rows,dtype=np.dtype(props))
        else:raise ValueError('Supported PLY encodings: ascii, binary_little_endian')
    return result

def convert_ply(path,output,transform,max_splats=30000):
    if not 1<=max_splats<=200000:raise ValueError('max_splats must be 1..200000')
    matrix=np.asarray(transform,dtype=float)
    if matrix.shape!=(4,4) or not np.isfinite(matrix).all():raise ValueError('Expected finite 4x4 transform')
    if not np.allclose(matrix[3],[0,0,0,1]):raise ValueError('Transform must be affine')
    linear=matrix[:3,:3];scale=np.linalg.norm(linear,axis=0)
    if min(scale)<=0 or not np.allclose(scale,scale[0],rtol=1e-4) or not np.allclose((linear/scale).T@(linear/scale),np.eye(3),atol=1e-4):raise ValueError('PLY transform must have uniform scale and orthogonal axes')
    basis=linear/scale[0]
    data=read_ply(path);names=set(data.dtype.names)
    required={'x','y','z','scale_0','scale_1','scale_2','rot_0','rot_1','rot_2','rot_3','opacity','f_dc_0','f_dc_1','f_dc_2'}
    if not required<=names:raise ValueError('Expected trained Gaussian PLY, missing '+str(sorted(required-names)))
    source_count=len(data)
    # Deterministic uniform subset; not an optimized level of detail.
    data=data[np.linspace(0,len(data)-1,min(max_splats,len(data)),dtype=np.int64)]
    with open(output,'wb') as f:
        f.write(b'WIG1');f.write(struct.pack('<i',len(data)))
        for row in data:
            raw=np.array([row[k] for k in required],dtype=float)
            if not np.isfinite(raw).all():raise ValueError('Nonfinite PLY value')
            center=linear@np.array([row['x'],row['y'],row['z']])+matrix[:3,3]
            scales=np.exp(np.clip([row['scale_0'],row['scale_1'],row['scale_2']],-20,10))*scale[0]
            q=np.array([row['rot_'+str(i)] for i in range(4)],dtype=float)
            if np.linalg.norm(q)<1e-8:raise ValueError('Zero PLY quaternion')
            rotation=trimesh.transformations.quaternion_matrix(q)[:3,:3]
            # Keep each Gaussian's local principal-axis scales unchanged. A reflected
            # world basis needs one local axis sign flip to recover a proper rotation.
            local_reflection=np.diag([-1.,1.,1.]) if np.linalg.det(basis)<0 else np.eye(3)
            converted=np.eye(4);converted[:3,:3]=basis@rotation@local_reflection
            qw,qx,qy,qz=trimesh.transformations.quaternion_from_matrix(converted)
            rgb=np.clip(.5+.28209479177387814*np.array([row['f_dc_'+str(i)] for i in range(3)]),0,1)
            opacity=1/(1+math.exp(-float(np.clip(row['opacity'],-80,80))))
            f.write(struct.pack('<14f',*center,*scales,qx,qy,qz,qw,*rgb,opacity))
    return {'sourceCount':source_count,'exportedCount':len(data),'appearance':'SH degree 0 only','sampling':'deterministic uniform subset','transform':matrix.tolist()}

def write_mesh(mesh,path):
    vertices=np.asarray(mesh.vertices,dtype='<f4');normals=np.zeros_like(vertices)
    faces=np.asarray(mesh.faces)
    face_normals=np.cross(vertices[faces[:,1]]-vertices[faces[:,0]],vertices[faces[:,2]]-vertices[faces[:,0]])
    for corner in range(3):np.add.at(normals,faces[:,corner],face_normals)
    lengths=np.linalg.norm(normals,axis=1,keepdims=True);normals/=np.maximum(lengths,1e-12)
    try:colors=np.asarray(mesh.visual.to_color().vertex_colors,dtype=np.float32)/255.
    except (AttributeError,ValueError):colors=np.asarray(mesh.visual.vertex_colors,dtype=np.float32)/255.
    if len(colors)!=len(vertices):colors=np.ones((len(vertices),4),dtype=np.float32)*.7;colors[:,3]=1
    uv=np.zeros((len(vertices),2),dtype=np.float32)
    indices=np.asarray(mesh.faces,dtype='<i4').reshape(-1)
    with open(path,'wb') as f:
        f.write(b'WIM1');f.write(struct.pack('<ii',len(vertices),len(indices)))
        f.write(np.hstack([vertices,normals,uv,colors]).astype('<f4').tobytes());f.write(indices.tobytes())

def prepare(scene_path,output,source_up='z',max_objects=128):
    path=Path(scene_path).resolve()
    if path.is_dir():path=path/'scene_with_all_objects.glb'
    scene=trimesh.load(path,force='scene',process=False)
    output=Path(output);output.mkdir(parents=True,exist_ok=True)
    nodes=sorted(scene.graph.nodes_geometry)
    if len(nodes)>max_objects:raise ValueError(f'{len(nodes)} mesh nodes exceeds max_objects={max_objects}; increase explicitly')
    manifest={'version':1,'name':path.parent.name,'sourceHash':sha256(path),'coordinateSystem':'Unity left-handed Y-up metres','sourceUp':source_up,'objects':[], 'notes':['GLB mesh nodes are provisional object boundaries; review before interaction.','Textures are sampled to vertex colors; this is not lossless material import.','Bounding-box collider candidates remain unverified until calibration.','Whole-scene GS is not automatically segmented or aligned.']}
    for number,node in enumerate(nodes):
        transform,geometry=scene.graph[node];mesh=scene.geometry[geometry].copy();mesh.apply_transform(AXES[source_up]@transform)
        if not np.isfinite(mesh.vertices).all():raise ValueError('Nonfinite mesh vertices')
        center=mesh.bounds.mean(axis=0);mesh.apply_translation(-center)
        name=f'{number:04d}_{safe_name(node)}';filename=name+'.mesh.bytes';write_mesh(mesh,output/filename)
        structure=any(word in node.lower() for word in ['wall','floor','ceiling','structure'])
        manifest['objects'].append({'id':name,'sourceNode':str(node),'mesh':filename,'gaussians':'','position':dict(zip('xyz',center.tolist())),'size':dict(zip('xyz',mesh.extents.tolist())),'category':'structure' if structure else 'prop','movable':False,'target':False,'support':False,'qualityVerified':False,'contactErrorMm':1000,'hiddenSurfacesComplete':False})
    (output/'world-interaction.json').write_text(json.dumps(manifest,indent=2,ensure_ascii=False),encoding='utf-8')
    return manifest

def main():
    parser=argparse.ArgumentParser(description=__doc__);sub=parser.add_subparsers(dest='command',required=True)
    p=sub.add_parser('scene');p.add_argument('--scene',required=True,type=Path);p.add_argument('--output',required=True,type=Path);p.add_argument('--source-up',choices=['y','z'],default='z');p.add_argument('--max-objects',type=int,default=128)
    p=sub.add_parser('ply');p.add_argument('--input',type=Path,required=True);p.add_argument('--output',type=Path,required=True);p.add_argument('--transform',type=Path,required=True,help='Explicit 4x4 PLY-to-Unity matrix JSON; do not assume the GLB frame');p.add_argument('--max-splats',type=int,default=30000)
    args=parser.parse_args()
    if args.command=='scene':
        result=prepare(args.scene,args.output,args.source_up,args.max_objects);print(f'Prepared {len(result["objects"])} mesh nodes in {args.output}')
    else:
        args.output.parent.mkdir(parents=True,exist_ok=True)
        result=convert_ply(args.input,args.output,json.loads(args.transform.read_text()),args.max_splats)
        args.output.with_suffix('.metadata.json').write_text(json.dumps(result,indent=2));print(json.dumps(result,indent=2))
if __name__=='__main__':main()
