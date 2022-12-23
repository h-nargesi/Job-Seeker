from enum import Enum

class Action(Enum):
    go = 1
    open = 2
    fill = 3
    click = 4
    close = 5
    wait = 6

    def str(self):
        return str(self)[7:].lower()
