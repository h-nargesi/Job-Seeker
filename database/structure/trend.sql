drop table if exists Trend;

create table Trend (
	TrendID			integer 	not null	primary key,
	AgencyID		integer		not null,
	Type			text		not null,
	State			text		not null,
	LastActivity	timestamp	not null	default current_timestamp,
	Reserved		bit			not null	default 0,
	
	unique			(AgencyID, Type),
	foreign key		(AgencyID) references Agency (AgencyID) on delete no action
)