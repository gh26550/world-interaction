import json,struct,tempfile,unittest
from pathlib import Path
import numpy as np
import trimesh
from prepare_worldmesh import AXES,convert_ply,prepare,read_ply
from analyze_results import analyze

class PipelineTests(unittest.TestCase):
    def setUp(self):self.temp=tempfile.TemporaryDirectory();self.root=Path(self.temp.name)
    def tearDown(self):self.temp.cleanup()
    def ply(self):
        p=self.root/'sample.ply';fields=['x','y','z','scale_0','scale_1','scale_2','rot_0','rot_1','rot_2','rot_3','opacity','f_dc_0','f_dc_1','f_dc_2']
        p.write_text('ply\nformat ascii 1.0\nelement vertex 1\n'+''.join(f'property float {x}\n' for x in fields)+'end_header\n1 2 3 -4 -3 -2 1 0 0 0 0 0 0 0\n');return p
    def test_gaussian_axis_and_anisotropy(self):
        p=self.ply();out=self.root/'g.bytes';convert_ply(p,out,AXES['z'],10);f=struct.unpack('<14f',out.read_bytes()[8:]);self.assertEqual(f[:3],(1,3,2));self.assertAlmostEqual(f[3],np.exp(-4),places=6);self.assertEqual(f[13],.5)
    def test_invalid_transform_rejected(self):
        with self.assertRaises(ValueError):convert_ply(self.ply(),self.root/'g',np.diag([2,1,1,1]))
    def test_covariance_preserved_under_axis_reflection(self):
        output=self.root/'g.bytes';convert_ply(self.ply(),output,AXES['z']);f=struct.unpack('<14f',output.read_bytes()[8:])
        rotation=trimesh.transformations.quaternion_matrix([f[9],f[6],f[7],f[8]])[:3,:3]
        actual=rotation@np.diag(np.square(f[3:6]))@rotation.T
        basis=AXES['z'][:3,:3];expected=basis@np.diag(np.exp(2*np.array([-4,-3,-2])))@basis.T
        np.testing.assert_allclose(actual,expected,rtol=1e-5,atol=1e-8)
    def test_truncated_ply_rejected(self):
        p=self.ply();p.write_text(p.read_text().replace('vertex 1','vertex 2'))
        with self.assertRaises(ValueError):read_ply(p)
    def test_mesh_conversion_keeps_units_and_marks_unverified(self):
        mesh=trimesh.creation.box(extents=[2,4,6]);path=self.root/'test.glb';mesh.export(path)
        result=prepare(path,self.root/'out','z');entry=result['objects'][0]
        self.assertEqual(list(entry['size'].values()),[2,6,4]);self.assertFalse(entry['qualityVerified']);self.assertEqual((self.root/'out'/entry['mesh']).read_bytes()[:4],b'WIM1')
    def test_results_separate_tasks_and_devices(self):
        for n,task in enumerate(['Observe','Transport']):
            d=self.root/str(n);d.mkdir();(d/'summary.json').write_text(json.dumps(dict(device='PC',scene='test',task=task,policy='MeshOnly',success=True,durationSeconds=2,p95FrameMs=10,requirementViolations=0,frames=100)))
        self.assertEqual(len(analyze(self.root)),2)
if __name__=='__main__':unittest.main()
