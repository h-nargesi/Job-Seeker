update Agency
set Settings = json_insert(Settings, '$.methods[#]',
    json('{ "Title": "FI", "Url": "https://fi.indeed.com/" }'))
where Title = 'Indeed'
  and Settings is not null
  and json_extract(Settings, '$.methods') not like '%fi.indeed.com%';

update Agency
set Settings = json_insert(Settings, '$.methods[#]',
    json('{ "Title": "FI", "Url": "&f_WT=2&f_E=3%2C4&location=Finland" }'))
where Title = 'LinkedIn'
  and Settings is not null
  and json_extract(Settings, '$.methods') not like '%location=Finland%';
