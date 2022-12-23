import logging
import json
from django.http import HttpResponse
from django.core.handlers.wsgi import WSGIRequest
from django.db import connection
from analyze.analyzer import Analyzer
from analyze.agency import Agency

logger = logging.getLogger('app-logger')

def take(request : WSGIRequest):

    data = json.loads(request.body.decode('utf-8'))

    if len(data) == 0:
        return HttpResponse(json.dumps("Invalid request"), content_type="application/json")

    eventlog(data)

    context = Analyzer.Analyze(data["agency"], data["url"], data["content"])

    return HttpResponse(json.dumps(context), content_type="application/json")

def eventlog(data):

    log_info = ""
    for p in data:
        if str(p).lower() != 'content':
            log_info += ", " + p + ":" + str(data[p])

    if len(log_info) == 0: return

    logger.info(log_info[2:])

def scopes(_):

    agencies = Agency.GetByName()

    context = []
    with connection.cursor() as cursor:
        cursor.execute(q_agancies)
        rows = cursor.fetchall()
        for row in rows:
            agency = {}; i = 0
            for cell in row:
                agency[cursor.description[i][0].lower()] = cell
                i += 1
            if agency["title"] not in agencies: continue
            context.append(agency)

    return HttpResponse(json.dumps(context), content_type="application/json")

q_agancies = "SELECT Title, Domain FROM Agency WHERE Active = 1"