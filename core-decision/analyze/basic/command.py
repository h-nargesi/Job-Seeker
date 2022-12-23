from json import JSONEncoder
from analyze.basic.action import Action

class Command(JSONEncoder):

    def __init__(self, action:Action, object:str, params:dict) -> None:
        self.__action = action
        self.__object = object
        self.__params = params

    @property
    def action(self) -> Action : return self.__action

    @property
    def object(self) -> str : return self.__object

    @property
    def params(self) -> dict : return self.__params

    def default(self, o):
        return o.__dict__ 

    def DictCommand(action:Action, object:str, params:dict) -> dict:
        return { "action": action.str(), "object": object, "params": params }
    
    def Go(url):
        return Command.DictCommand(Action.go, None, { "url": url })

    def Open(url):
        return Command.DictCommand(Action.open, None, { "url": url })

    def Fill(object, value):
        return Command.DictCommand(Action.fill, object, { "value" : value })

    def Click(object):
        return Command.DictCommand(Action.click, object, None)

    def Close():
        return Command.DictCommand(Action.close, None, None)

    def Wait(miliseconds:int):
        return Command.DictCommand(Action.wait, None, { "miliseconds": miliseconds })