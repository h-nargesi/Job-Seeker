update Job
set State = 'Saved',
    Attempts = 0,
    Tries = null,
    ModifiedOn = datetime('now')
where State = 'NotApprovedRegex'
  and Html is null
  and Content is null;
