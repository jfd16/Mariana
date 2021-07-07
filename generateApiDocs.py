import subprocess, os, shutil, sys, tempfile

from argparse import ArgumentParser
from os import path

if (__name__ == "__main__"):

    argParser = ArgumentParser(description = "Generate API documentation")
    argParser.add_argument("--docfx-path", help = "Path to docfx.exe")
    argParser.add_argument("--output", help = "Path to generated API documentation", required = True)

    parsedArgs = argParser.parse_args()

    docfxPath = parsedArgs.docfx_path or shutil.which("docfx")
    if (docfxPath is None):
        raise RuntimeError("Unable to find docfx. Specify docfx.exe location with --docfx-path if not on PATH.")

    tempDir = tempfile.mkdtemp()

    try:
        with open(path.join(tempDir, "docfx.json"), "w") as configFile:
            # Create minimal docfx.json as we will pass all options as command line arguments
            configFile.write('{"build": { }, "metadata": { }}')

        with open(".gitignore", "r") as gitignoreFile:
            gitignores = {
                line
                for line in (_line.strip() for _line in gitignoreFile.readlines())
                if len(line) != 0 and line.find('/') == -1 and line.find('*') == -1
            }

        # Find all .csproj files
        projects = []

        for subdir in os.scandir("."):
            if (subdir.name.endswith(".Tests") or subdir.name in gitignores):
                # Exclude test projects and those in .gitignore

                continue

            csprojPath = path.join(subdir.name, subdir.name + ".csproj")
            if (not path.exists(csprojPath)):
                # Directory is not a project
                continue

            projects.append(path.abspath(csprojPath))

        process = subprocess.run(
            [docfxPath, "metadata", "--output", "__api", *projects],
            shell = False,
            stdout = sys.stdout,
            stderr = sys.stderr,
            check = True,
            cwd = tempDir
        )

        shutil.move(path.join(tempDir, "__api", "__api"), path.join(tempDir, "api"))

        process = subprocess.run(
            [
                docfxPath,
                "build",
                "--content", "api/**.yml",
                "--template", "statictoc",
                "--output", "site",
                "--markdownEngineName", "markdig"
            ],
            shell = False,
            stdout = sys.stdout,
            stderr = sys.stderr,
            check = True,
            cwd = tempDir
        )

        if (path.exists(parsedArgs.output)):
            shutil.rmtree(parsedArgs.output)

        shutil.move(path.join(tempDir, "site"), parsedArgs.output)

    finally:
        shutil.rmtree(tempDir)
