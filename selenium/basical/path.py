def __Get_Program_Path():
    from os.path import abspath
    from inspect import getsourcefile
    from pathlib import Path

    return str(Path(abspath(getsourcefile(lambda:0))).parent.parent.absolute())

BRAVE_LOCATION = "/snap/brave/194/opt/brave.com/brave/brave-browser"
DRIVER_PATH = __Get_Program_Path() + "/driver/chromedriver"
