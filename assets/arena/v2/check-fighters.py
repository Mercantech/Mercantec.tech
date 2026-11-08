from PIL import Image
import os
root=os.path.abspath(os.path.join(os.path.dirname(__file__),'../../..'))
canvas=Image.new('RGBA',(1024,1024),(10,24,48,255))
im=Image.open(os.path.join(root,'web/wwwroot/assets/arena/v2/fighters-starter.webp')).convert('RGBA')
canvas.alpha_composite(im)
canvas.convert('RGB').save(os.path.join(root,'assets/arena/v2/fighter-starter-proof.jpg'),quality=95)
