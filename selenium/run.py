import time
import json
import random
import re
import threading

import basical.infra_packages as inf
import basical.path as PATH

inf.install_packages()

import requests
from selenium.webdriver.common.by import By
from selenium.webdriver.support.ui import WebDriverWait
from selenium.webdriver.support import expected_conditions as EC
from seleniumwire import webdriver as wirewebdriver
from selenium.common.exceptions import TimeoutException


def run():
    # import webbrowser
    # webbrowser.open('http://google.com')

    chrome_options = wirewebdriver.ChromeOptions()
    chrome_options.binary_location = PATH.BRAVE_LOCATION
    # chrome_options.add_argument("--incognito")
    chrome_options.add_argument("--start-maximized")
    # chrome_options.add_argument('ignore-certificate-errors')
    # chrome_options.add_argument('--ignore-ssl-errors=yes')
    # chrome_options.add_argument('--ignore-certificate-errors')
    # chrome_options.add_argument('--allow-insecure-localhost')
    # chrome_options.accept_insecure_certs = True
    # chrome_options.add_experimental_option("excludeSwitches", ["enable-logging"])
    # caps = chrome_options.to_capabilities()
    # caps["acceptInsecureCerts"] = True

    driver = wirewebdriver.Chrome(PATH.DRIVER_PATH, chrome_options=chrome_options)
    while(True):
        try:
            driver.get('https://www.indeed.com')
            wait = WebDriverWait(driver, 10)
            wait.until(EC.frame_to_be_available_and_switch_to_it((By.TAG_NAME, 'iframe')))
        except:
            print('Error: Cannot establish a connection to indeed.com.')
            time.sleep(5)
        else:
            break

    form = driver.find_elements(By.LINK_TEXT, 'Sign In')

    for f in form:
        print(f.tag_name, f.text)

run()