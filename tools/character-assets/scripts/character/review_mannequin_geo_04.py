"""Local visual comparison against the owner's blond-warrior reference."""
import hashlib
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/"outputs/prototypes/mannequin_geo_04"
REF=ROOT/"references/characters_v1/human_warrior_reference_768x1024.png"
assert hashlib.sha256(REF.read_bytes()).hexdigest()=="569ef0b5a79b24fe9fbb886508a06d50900c25f91738699c0b3bfdb4843d1a08"
font=ImageFont.truetype("segoeui.ttf",22)
title=ImageFont.truetype("segoeuib.ttf",33)
canvas=Image.new("RGB",(1530,749),"#171d23")
draw=ImageDraw.Draw(canvas)
draw.text((22,14),"Манекен редактора персонажа",font=title,fill="#f1ede7")
# Scale reference by chin-to-ground length. Hair and equipment stay in the reference panel.
reference=Image.open(REF).convert("RGB")
reference=reference.crop((70,250,678,1008))
reference.thumbnail((498,620),Image.Resampling.LANCZOS)
tile=Image.new("RGB",(498,620),reference.getpixel((0,0)))
tile.paste(reference,((498-reference.width)//2,620-reference.height))
tiles=[tile]
for filename in ("EDITOR_FRONT","EDITOR_34"):
    im=Image.open(OUT/(filename+".png")).convert("RGB")
    panel=Image.new("RGB",(498,620),im.getpixel((0,0)))
    panel.paste(im.resize((498,598),Image.Resampling.LANCZOS),(0,32))
    tiles.append(panel)
x=12
for tile,label in zip(tiles,("Референс владельца","Основа · спереди","Основа · три четверти")):
    canvas.paste(tile,(x,68))
    draw.text((x+6,702),label,font=font,fill="#edf0f1")
    x+=tile.width+5
canvas.save(OUT/"blond_comparison.jpg",quality=94,subsampling=0)


def sheet(filename, entries, columns, width=245):
    height=round(width*1.2)
    rows=(len(entries)+columns-1)//columns
    page=Image.new("RGB",((width+10)*columns+10,(height+40)*rows+10),"#171d23")
    draw=ImageDraw.Draw(page)
    for i,(path,label) in enumerate(entries):
        x=10+(i%columns)*(width+10)
        y=10+(i//columns)*(height+40)
        im=Image.open(path).convert("RGB")
        page.paste(im.resize((width,height),Image.Resampling.LANCZOS),(x,y))
        draw.text((x+2,y+height+3),label,font=font,fill="#eff0ed")
    page.save(OUT/filename,quality=93,subsampling=0)


sheet("views.jpg",[(OUT/(s+".png"),s.replace("_"," ")) for s in (
    "FRONT","BACK","LEFT","RIGHT","THREE_QUARTER","IDLE","WALK","ATTACK")],4)
sheet("details.jpg",[(OUT/(s+".png"),s.replace("_"," ")) for s in (
    "HEAD_FRONT","EAR_SIDE","HEAD","HAND_OPEN","HAND_FIST","HAND_GRIP","FEET_FRONT","FEET_SIDE","FOOT")],3)
old=ROOT/"outputs/prototypes/mannequin_geo_03/run1/renders"
sheet("geo03_geo04.jpg",[(directory/(name+".png"),rev+" · "+name) for name in
     ("FRONT","THREE_QUARTER","IDLE") for directory,rev in ((old,"GEO-03"),(OUT,"GEO-04"))],2)
