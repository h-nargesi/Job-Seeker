create index if not exists IX_Job_State on Job (State);
create index if not exists IX_Job_RegTime on Job (RegTime);
create index if not exists IX_Job_ModifiedOn on Job (ModifiedOn);
create index if not exists IX_Job_AiVerdict on Job (AiVerdict);
create index if not exists IX_AiRun_Recent on AiRun (StartedUtc desc, RunID desc);
