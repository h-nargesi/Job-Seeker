import logging
from analyze.basic.importall import ImportAll
from analyze.agency import Agency
from analyze.iamexpat.iamexpat_page import IamExpatPage

class IamExpat(Agency):

    __logger = logging.getLogger('app-logger')

    @property
    def Name(self) -> str : return "IamExpat"

    def page_subclasses(self):
        ImportAll(__file__, self.__logger)
        return IamExpatPage.__subclasses__()