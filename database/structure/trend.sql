create table Trend (
	TrendID			integer 	not null	primary key,
	AgencyID		integer		not null,
	LastActivity	datetime	not null	default current_timestamp,
	State			text		not null,
	
	foreign key		(AgencyID) references Agency (AgencyID) on delete no action
)