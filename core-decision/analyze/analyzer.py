import logging
from analyze.basic.importall import SaveHtmlContent
from analyze.agency import Agency

class Analyzer:

    __logger = logging.getLogger('app-logger')

    def Analyze(agency:str, url:str, content:str) -> list:
        Analyzer.__logger.debug('Analyzer.Analyze: ' + agency)

        agencies = Agency.GetByName()

        if agency in agencies:
            SaveHtmlContent(__file__, content)
            return agencies[agency].AnalyzeContent(url, content)
        
        else: Analyzer.__logger.error(agency + ' not found!')

        return []