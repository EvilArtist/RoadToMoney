# Create Godot project folder structure

$folders = @(
    "scenes\world",
    "scenes\player",
    "scenes\creatures",
    "scenes\ui",
    "scenes\props",

    "scripts\autoloads",
    "scripts\player",
    "scripts\creatures",
    "scripts\ui",
    "scripts\systems",

    "resources\items",
    "resources\upgrades",
    "resources\creatures",

    "assets\models",
    "assets\textures",
    "assets\sounds",
    "assets\particles",
    "assets\shaders",

    "addons"
)

foreach ($folder in $folders) {
    New-Item -Path $folder -ItemType Directory -Force | Out-Null
}

Write-Host "Folder structure created successfully." -ForegroundColor Green