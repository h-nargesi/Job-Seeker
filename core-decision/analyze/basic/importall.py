from os.path import dirname, basename, isfile, join
import glob
import os
import importlib

def ImportModules(directory, logger):
    directory = dirname(directory)
    packages = [join(dir[0], basename(dir[0])+ ".py") for dir in os.walk(directory)]
    packages = [basename(f)[:-3] for f in packages if isfile(f)]
    packages = ['analyze.' + pg + '.' + pg for pg in packages]
    logger.info(packages)
    for package in packages:
        importlib.import_module(package)

def ImportAll(directory, logger):
    directory = dirname(directory)
    modules = [join(dir[0], "*.py") for dir in os.walk(directory)]
    packages = []
    for m in modules:
        packages = packages + glob.glob(m)
    packages = [basename(f)[:-3] for f in packages if isfile(f)]
    packages = ['analyze.' + basename(directory) + '.' + pg for pg in packages]
    logger.info(packages)
    for package in packages:
        importlib.import_module(package)

def SaveHtmlContent(directory, content):
    with open(join(dirname(directory), '../logs/content.html'), "a") as f:
        f.seek(0)
        f.write(content)
        f.truncate()
