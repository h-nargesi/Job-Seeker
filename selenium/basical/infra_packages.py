
def __install(package):
    import importlib
    try:
        importlib.import_module(package.replace('-', ''))
        print("The '{}' is already installed".format(package))
    except ImportError:
        import pip
        print("Installing '{}' ...".format(package))
        pip.main(['install', package])

def install_packages():
    __install('requests')
    __install('selenium')
    __install('selenium-wire')