"""Orthographic preview of the original parametric prototype. Requires matplotlib."""
from pathlib import Path
import sys
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from mpl_toolkits.mplot3d.art3d import Poly3DCollection
sys.path.insert(0,str(Path(__file__).resolve().parent))
from orc_prototype import make_orc

m=make_orc()
vertsigns=[(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]
faces=[(0,1,2,3),(4,5,6,7),(0,1,5,4),(3,2,6,7),(0,3,7,4),(1,2,6,5)]
fig=plt.figure(figsize=(12,5),facecolor='#e4e2db')
for n,(az,el,name) in enumerate([(-90,8,'Front'),(-55,13,'Three-quarter'),(0,9,'Side')]):
 ax=fig.add_subplot(1,3,n+1,projection='3d',proj_type='ortho')
 ax.set_facecolor('#e4e2db')
 polygons=[];cols=[]
 for part,boxes in m.cubes.items():
  for cen,sz,color in boxes:
   pts=[tuple(cen[i]+s[i]*sz[i]/2 for i in range(3)) for s in vertsigns]
   for face in faces:
    # Original Y-up -> display Z-up and front Z-positive becomes plot Y-negative
    polygons.append([(pts[j][0],-pts[j][2],pts[j][1]) for j in face])
    shade=[.85,1,.8,.9,.72,.82][faces.index(face)]
    cols.append(tuple(min(1,color[i]/255*shade+.07) for i in range(3)))
 ax.add_collection3d(Poly3DCollection(polygons,facecolors=cols,edgecolors=(.08,.08,.08,.35),linewidths=.12,zsort='average'))
 ax.set_xlim(-1.45,1.45);ax.set_ylim(-1.2,1.2);ax.set_zlim(0,3.25)
 ax.set_box_aspect((2.9,2.4,3.25));ax.view_init(elev=el,azim=az)
 ax.set_axis_off();ax.set_title(name,fontsize=12)
plt.tight_layout();out=Path(__file__).resolve().parents[2]/'outputs/prototypes/orc_modular_v01_preview.png'
fig.savefig(out,dpi=140,bbox_inches='tight');print(out)
