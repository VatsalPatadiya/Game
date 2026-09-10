const sharp = require('sharp');
const fs = require('fs');

async function convert(file) {
    await sharp(file + '.svg')
        .resize(256, 256)
        .png()
        .toFile(file + '.png');
    console.log('Converted ' + file);
}

['icon_hint', 'icon_shuffle', 'icon_undo'].forEach(f => {
    convert('Assets/Textures/HudIcons/' + f);
});
