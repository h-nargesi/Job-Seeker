import logging
from analyze.basic.importall import ImportModules, SaveHtmlContent
from analyze.basic.command import Command
from django.db import connection

class Agency:

    __agencies_name : dict = None
    __agencies_id : dict = None
    __pages : list = None
    __logger = logging.getLogger('app-logger')

    def __init__(self) -> None:
        pass

    @property
    def Name(self) -> str : pass

    @property
    def ID(self) -> int : pass

    def GetByName():
        if Agency.__agencies_name is None:
            Agency.__load_agencies()
        return Agency.__agencies_name

    def GetByID():
        if Agency.__agencies_id is None:
            Agency.__load_agencies()
        return Agency.__agencies_id

    def Pages(self) -> list :
        if self.__pages is None:
            self.__load_pages()
        return self.__pages

    def AnalyzeContent(self, url:str, content:str) -> list :
        self.__logger.debug('AnalyzeContent: ' + self.Name)

        for page in self.Pages():
            commands = page.IssueCommand(url, content)
            if commands is not None:
                self.__logger.info("page checked: " + page.__class__.__name__)
                self.__logger.debug("page commands: " + commands)
                return commands
        
        self.__logger.debug('Page not found: ' + self.Name)
        return [Command.Close()]

    def page_subclasses(self):
        pass

    def __load_pages(self):
        self.__logger.debug('loading pages of ' + self.Name)
        self.__pages = []
        for page_type in self.page_subclasses():
            page = page_type()
            if page.Order is None: continue
            page.Parent = self
            self.__pages.append(page)
            self.__logger.debug("page added: " + page_type.__name__)
        
        self.__pages.sort(key=lambda x: x.Order)
        self.__logger.info("pages: " + str([type(p).__name__ for p in self.__pages]))

    def __load_agencies():
        Agency.__logger.debug('loading agencies')
        ImportModules(__file__, Agency.__logger)
        Agency.__agencies_name = {}
        Agency.__agencies_id = {}
        for agency_type in Agency.__subclasses__():
            agency = agency_type()
            if agency.Name is None: continue
            Agency.__agencies_name[agency.Name] = agency
            Agency.__agencies_id[agency.ID] = agency
            Agency.__logger.debug("agency added: " + agency.Name)